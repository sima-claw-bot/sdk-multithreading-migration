using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
        // Correct: uses TaskEnvironment for path resolution
        PathResult = TaskEnvironment.GetAbsolutePath(InputPath).Value;

        // BUG: still reads from process-global Environment instead of TaskEnvironment
        EnvResult = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;
        return true;
    }
}
