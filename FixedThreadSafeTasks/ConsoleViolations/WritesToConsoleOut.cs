using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Fixed version: uses Log.LogMessage instead of Console.WriteLine to write output
/// through the MSBuild logging infrastructure rather than the process-global Console.Out.
/// </summary>
[MSBuildMultiThreadableTask]
public class WritesToConsoleOut : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Message { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Log.LogMessage(MessageImportance.Normal, Message);
        Result = Message;
        return true;
    }
}
