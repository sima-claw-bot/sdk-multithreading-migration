using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
