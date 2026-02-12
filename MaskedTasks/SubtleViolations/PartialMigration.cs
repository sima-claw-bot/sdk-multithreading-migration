using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.SubtleViolations;

/// <summary>
/// Partially migrated task: implements IMultiThreadableTask and uses TaskEnvironment
/// for path resolution (correct), but still reads environment variables through
/// the process-global Environment API (incorrect). One code path is safe, one is not.
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
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
