using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Redirects Console.SetOut to a custom writer, corrupting the global Console state.
/// This is unsafe because Console.Out is process-global shared state and redirecting it
/// affects all other tasks and code running in the same process.
/// </summary>
public class UsesConsoleSetOut : Task
{
    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        var writer = new StringWriter();

        // BUG: corrupts global Console.Out for all concurrent tasks
        Console.SetOut(writer);
        Console.WriteLine("captured");
        Console.Out.Flush();

        Result = writer.ToString().Trim();
        return true;
    }
}
