using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: both paths are now resolved via TaskEnvironment.GetAbsolutePath.
/// </summary>
[MSBuildMultiThreadableTask]
public class PartialMigration : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string SourcePath { get; set; } = string.Empty;

    [Required]
    public string DestinationPath { get; set; } = string.Empty;

    [Output]
    public string ResolvedSource { get; set; } = string.Empty;

    [Output]
    public string ResolvedDestination { get; set; } = string.Empty;

    public override bool Execute()
    {
        ResolvedSource = TaskEnvironment.GetAbsolutePath(SourcePath);
        ResolvedDestination = TaskEnvironment.GetAbsolutePath(DestinationPath);
        return true;
    }
}
