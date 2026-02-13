using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: captures the project directory from TaskEnvironment eagerly before
/// queuing the ThreadPool work item. The callback uses the captured directory instead of
/// reading the process-global CWD at an indeterminate time.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskAlpha11 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string RelativeFilePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedFilePath { get; set; } = string.Empty;

    [Output]
    public bool FileFound { get; set; }

    public override bool Execute()
    {
        using var done = new ManualResetEventSlim(false);
        string? resolvedPath = null;
        bool found = false;

        // Fixed: capture the absolute path eagerly from TaskEnvironment before queuing.
        string absolutePath = TaskEnvironment.GetAbsolutePath(RelativeFilePath);

        ThreadPool.QueueUserWorkItem(_ =>
        {
            // Fixed: use the pre-resolved absolute path instead of reading CWD.
            found = File.Exists(absolutePath);
            resolvedPath = absolutePath;
            done.Set();
        });

        done.Wait();

        ResolvedFilePath = resolvedPath ?? string.Empty;
        FileFound = found;
        return true;
    }
}
