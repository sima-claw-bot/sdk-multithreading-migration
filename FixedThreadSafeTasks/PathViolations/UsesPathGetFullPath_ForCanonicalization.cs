using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.PathViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath
/// for canonicalization.
/// </summary>
public class UsesPathGetFullPath_ForCanonicalization : Task, IMultiThreadableTask
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
