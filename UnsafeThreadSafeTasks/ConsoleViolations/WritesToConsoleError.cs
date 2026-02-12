using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Writes error output via Console.Error.WriteLine. This is unsafe because
/// Console.Error is process-global shared state that can be redirected by
/// other concurrent tasks.
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
