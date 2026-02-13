using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
