using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Fixed version: sets and reads environment variables via TaskEnvironment instead of
/// the process-global Environment.SetEnvironmentVariable/GetEnvironmentVariable.
/// </summary>
[MSBuildMultiThreadableTask]
public class UsesEnvironmentSetVariable : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        TaskEnvironment.SetEnvironmentVariable(Name, Value);
        Result = TaskEnvironment.GetEnvironmentVariable(Name) ?? string.Empty;
        return true;
    }
}
