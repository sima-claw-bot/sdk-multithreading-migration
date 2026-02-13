using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class SetsConsoleOut : Task
{
    [Output]
    public string CapturedOutput { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            Console.WriteLine(Message);
            CapturedOutput = writer.ToString().Trim();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
        return true;
    }
}
