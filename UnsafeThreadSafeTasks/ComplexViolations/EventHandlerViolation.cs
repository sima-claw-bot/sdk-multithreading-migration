#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class EventHandlerViolation : Task
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
