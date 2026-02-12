using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Writes task output to Console.WriteLine instead of Log.LogMessage. This is unsafe because
/// Console output bypasses the MSBuild logging infrastructure, so the message is never captured
/// in MSBuild output or build logs.
/// </summary>
public class UsesConsoleWriteLine : Task
{
    [Required]
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: writes to Console instead of Log — invisible in MSBuild output
        Console.WriteLine(Message);
        return true;
    }
}
