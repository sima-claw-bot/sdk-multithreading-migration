using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Partially migrated task: implements IMultiThreadableTask and uses TaskEnvironment
/// for path resolution (correct), but still reads environment variables through
/// the process-global Environment API (incorrect). One code path is safe, one is not.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskTheta04 : Task, IMultiThreadableTask
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
