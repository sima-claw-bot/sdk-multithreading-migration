using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: uses Log.LogWarning instead of redirecting Console.Error via Console.SetError().
/// The message is routed through MSBuild's logging infrastructure and captured directly
/// without touching the process-global Console.Error stream.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskBeta01 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Output]
    public string CapturedOutput { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogWarning(Message);
        CapturedOutput = Message;
        return true;
    }
}
