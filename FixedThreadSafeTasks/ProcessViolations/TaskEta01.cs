using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ProcessViolations;

/// <summary>
/// Fixed version: logs an error and returns false instead of calling Environment.Exit().
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskEta01 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    public int ExitCode { get; set; }

    public override bool Execute()
    {
        Log.LogError("Tasks must not call Environment.Exit(). This would terminate the entire build process.");
        return false;
    }
}
