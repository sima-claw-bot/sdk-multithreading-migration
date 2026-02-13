using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class ThreadPoolViolation : Task
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
