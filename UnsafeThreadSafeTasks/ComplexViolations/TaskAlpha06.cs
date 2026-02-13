#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// Registers a static event handler that captures environment state (the process-wide CWD)
/// at invocation time. Because the handler is static, it persists across task executions and
/// fires with whatever CWD happens to be active, not the one that was active when the task
/// that registered it was running. This makes the resolved path nondeterministic under
/// concurrent builds.
/// </summary>
public class TaskAlpha06 : Task
{
    // BUG: static event shared across all task instances — handlers accumulate and
    // execute with the wrong CWD when multiple projects build concurrently.
    private static event EventHandler<string>? PathResolved;

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        string result = string.Empty;

        // BUG: The handler captures 'result' but resolves the path using the
        // process-global CWD at the time the event fires, not at registration time.
        PathResolved += (sender, path) =>
        {
            result = Path.GetFullPath(path);
        };

        // Fire the event — the CWD may have been changed by another concurrent task.
        PathResolved?.Invoke(this, RelativePath);

        ResolvedPath = result;
        return true;
    }
}
