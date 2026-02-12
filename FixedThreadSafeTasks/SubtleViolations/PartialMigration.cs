using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fully migrated version: uses TaskEnvironment for both path resolution and
/// environment variable access. All code paths are thread-safe.
/// </summary>
[MSBuildMultiThreadableTask]
public class PartialMigration : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string PathResult { get; set; } = string.Empty;

    [Output]
    public string EnvResult { get; set; } = string.Empty;

    public override bool Execute()
    {
        PathResult = TaskEnvironment.GetAbsolutePath(InputPath).Value;
        EnvResult = TaskEnvironment.GetEnvironmentVariable(VariableName) ?? string.Empty;
        return true;
    }
}
