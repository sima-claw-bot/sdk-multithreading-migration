using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: uses Log.LogMessage instead of Console.WriteLine so the message is
/// captured in MSBuild output and build logs rather than being written to process-global stdout.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskBeta05 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, Message);
        return true;
    }
}
