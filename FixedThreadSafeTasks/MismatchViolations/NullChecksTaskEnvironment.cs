using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.MismatchViolations;

/// <summary>
/// Fixed version: always uses TaskEnvironment.GetAbsolutePath without null-check fallback.
/// </summary>
[MSBuildMultiThreadableTask]
public class NullChecksTaskEnvironment : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.GetAbsolutePath(InputPath).Value;
        return true;
    }
}
