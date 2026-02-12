using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Writes output via Console.WriteLine. This is unsafe because Console.Out is
/// process-global shared state — concurrent tasks writing to Console.Out will
/// produce interleaved output.
/// </summary>
public class WritesToConsoleOut : Task
{
    [Required]
    public string Message { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Console.WriteLine(Message);
        Result = Message;
        return true;
    }
}
