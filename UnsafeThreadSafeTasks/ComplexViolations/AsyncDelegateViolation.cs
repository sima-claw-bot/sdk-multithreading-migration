using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class AsyncDelegateViolation : Task
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
