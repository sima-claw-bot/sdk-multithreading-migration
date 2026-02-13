using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class WritesToConsoleError : Task
{
    [Required]
    public string Message { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Console.Error.WriteLine(Message);
        Result = Message;
        return true;
    }
}
