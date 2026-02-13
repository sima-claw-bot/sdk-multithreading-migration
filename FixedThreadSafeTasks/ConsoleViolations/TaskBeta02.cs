using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: uses Log.LogMessage instead of redirecting Console.Out via Console.SetOut().
/// The message is routed through MSBuild's logging infrastructure and captured directly
/// without touching the process-global Console.Out stream.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskBeta02 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Output]
    public string CapturedOutput { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, Message);
        CapturedOutput = Message;
        return true;
    }
}
