using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Redirects Console.Error via Console.SetError(). This is unsafe because it changes
/// the process-global stderr stream, affecting all concurrent tasks that write
/// to Console.Error.
/// </summary>
public class TaskBeta01 : Task
{
    [Output]
    public string CapturedOutput { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        var originalError = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);
            Console.Error.WriteLine(Message);
            CapturedOutput = writer.ToString().Trim();
        }
        finally
        {
            Console.SetError(originalError);
        }
        return true;
    }
}
