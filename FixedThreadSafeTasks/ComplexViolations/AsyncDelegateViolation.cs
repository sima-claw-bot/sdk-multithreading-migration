using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: captures the project directory from TaskEnvironment at delegate-creation
/// time instead of reading Directory.GetCurrentDirectory() at delegate-execution time.
/// This ensures the resolved path is always relative to the correct project directory.
/// </summary>
[MSBuildMultiThreadableTask]
public class AsyncDelegateViolation : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Fixed: capture the project directory eagerly from TaskEnvironment.
        var projectDir = TaskEnvironment.ProjectDirectory;
        Func<string> resolver = () =>
        {
            return Path.Combine(projectDir, RelativePath);
        };

        var task = System.Threading.Tasks.Task.Run(resolver);
        task.Wait();

        Result = task.Result;
        return true;
    }
}
