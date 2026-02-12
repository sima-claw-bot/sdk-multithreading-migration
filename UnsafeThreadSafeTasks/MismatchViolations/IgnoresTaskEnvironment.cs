using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// Implements IMultiThreadableTask with TaskEnvironment property but never reads from it.
/// Instead it uses Path.GetFullPath directly, ignoring the thread-safe environment provided
/// by MSBuild. This is unsafe because the task opts in to multithreading but does not use
/// the facilities that make it safe.
/// </summary>
public class IgnoresTaskEnvironment : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: Should use TaskEnvironment.GetAbsolutePath(InputPath) instead
        Result = Path.GetFullPath(InputPath);
        return true;
    }
}
