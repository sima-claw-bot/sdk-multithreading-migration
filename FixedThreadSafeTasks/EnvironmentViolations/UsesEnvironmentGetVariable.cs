using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Fixed version: reads environment variables via TaskEnvironment.GetEnvironmentVariable
/// instead of the process-global Environment.GetEnvironmentVariable.
/// </summary>
[MSBuildMultiThreadableTask]
public class UsesEnvironmentGetVariable : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.GetEnvironmentVariable(VariableName) ?? string.Empty;
        return true;
    }
}
