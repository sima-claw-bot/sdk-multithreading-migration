using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Fixed version: reads the project directory from TaskEnvironment.ProjectDirectory
/// instead of the process-global Environment.CurrentDirectory.
/// </summary>
[MSBuildMultiThreadableTask]
public class ReadsEnvironmentCurrentDirectory : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.ProjectDirectory;
        return true;
    }
}
