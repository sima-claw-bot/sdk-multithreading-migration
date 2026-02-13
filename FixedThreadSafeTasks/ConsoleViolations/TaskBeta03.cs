using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: does not call Console.ReadLine which would block waiting for stdin input
/// and hang the entire build. Instead, logs a warning and returns a safe result.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskBeta03 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    public bool BlockingMode { get; set; }

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        if (!BlockingMode)
        {
            Result = "SKIPPED";
            return true;
        }

        Log.LogWarning("Console.ReadLine is not supported in multithreaded MSBuild tasks. Use task parameters instead.");
        Result = "SKIPPED";
        return true;
    }
}
