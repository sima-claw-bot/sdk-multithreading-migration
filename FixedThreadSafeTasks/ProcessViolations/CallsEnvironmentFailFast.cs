using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ProcessViolations;

/// <summary>
/// Fixed version: logs an error and returns false instead of calling Environment.FailFast().
/// </summary>
[MSBuildMultiThreadableTask]
public class CallsEnvironmentFailFast : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogError("Tasks must not call Environment.FailFast(). This would terminate the entire build process.");
        return false;
    }
}
