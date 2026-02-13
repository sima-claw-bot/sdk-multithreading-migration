using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: uses Log.LogMessage instead of redirecting Console.SetOut to a custom writer.
/// The result is set directly without corrupting the global Console.Out state.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskBeta04 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, "captured");
        Result = "captured";
        return true;
    }
}
