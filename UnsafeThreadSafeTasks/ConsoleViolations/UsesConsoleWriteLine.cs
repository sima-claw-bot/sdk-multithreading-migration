using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
