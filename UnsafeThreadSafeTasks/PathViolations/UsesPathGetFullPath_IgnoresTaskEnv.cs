using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// Implements IMultiThreadableTask but still calls Path.GetFullPath instead of
/// TaskEnvironment.GetAbsolutePath. This is unsafe because it ignores the task environment.
/// </summary>
public class UsesPathGetFullPath_IgnoresTaskEnv : Task, IMultiThreadableTask
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
