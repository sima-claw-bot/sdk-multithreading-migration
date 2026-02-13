using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// Captures the current working directory inside an async delegate. The delegate may
/// execute later on a different thread, by which time another task may have changed the
/// process-wide CWD. This is unsafe because the resolved path depends on whichever CWD
/// is active at delegate-execution time, not at capture time.
/// </summary>
public class TaskAlpha02 : Task
{
    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: The delegate captures 'this' but resolves the path lazily using
        // Directory.GetCurrentDirectory(), which is process-global and racy.
        Func<string> resolver = () =>
        {
            var cwd = Directory.GetCurrentDirectory();
            return Path.Combine(cwd, RelativePath);
        };

        // Simulate deferred execution — another task may change CWD before this runs.
        var task = System.Threading.Tasks.Task.Run(resolver);
        task.Wait();

        Result = task.Result;
        return true;
    }
}
