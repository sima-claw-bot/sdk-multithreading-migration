using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ProcessViolations;

/// <summary>
/// Fixed version: logs an error and returns false instead of calling Process.GetCurrentProcess().Kill().
/// </summary>
[MSBuildMultiThreadableTask]
public class CallsProcessKill : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    public override bool Execute()
    {
        Log.LogError("Tasks must not call Process.GetCurrentProcess().Kill(). This would terminate the entire build process.");
        return false;
    }
}
