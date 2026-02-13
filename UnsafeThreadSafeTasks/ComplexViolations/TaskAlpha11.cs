using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// Queues work items to the <see cref="ThreadPool"/> that resolve file paths using the
/// current working directory. This is unsafe because the thread-pool callback executes at
/// an indeterminate time on a pool thread, and by then the process-wide CWD may have been
/// changed by another task. The resolved paths therefore depend on timing rather than on
/// the originating project's directory.
/// </summary>
public class TaskAlpha11 : Task
{
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

        // BUG: The work item runs on a ThreadPool thread at an indeterminate time.
        // By the time it executes, another task may have changed the process CWD.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            // BUG: Directory.GetCurrentDirectory() is process-global and racy.
            var cwd = Directory.GetCurrentDirectory();
            var fullPath = Path.Combine(cwd, RelativeFilePath);

            // BUG: File.Exists on a relative-derived path — depends on CWD at
            // execution time, not at task-queue time.
            found = File.Exists(fullPath);
            resolvedPath = fullPath;
            done.Set();
        });

        done.Wait();

        ResolvedFilePath = resolvedPath ?? string.Empty;
        FileFound = found;
        return true;
    }
}
