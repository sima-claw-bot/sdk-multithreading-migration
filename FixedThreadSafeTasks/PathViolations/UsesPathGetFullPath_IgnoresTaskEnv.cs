using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.PathViolations;

/// <summary>
/// Fixed version: properly uses TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath.
/// </summary>
[MSBuildMultiThreadableTask]
public class UsesPathGetFullPath_IgnoresTaskEnv : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.GetAbsolutePath(InputPath);
        return true;
    }
}
