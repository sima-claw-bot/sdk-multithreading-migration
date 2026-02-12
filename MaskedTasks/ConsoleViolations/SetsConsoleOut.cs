using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ConsoleViolations;

/// <summary>
/// Redirects Console.Out via Console.SetOut(). This is unsafe because it changes
/// the process-global stdout stream, affecting all concurrent tasks that write
/// to Console.Out.
/// </summary>
public class SetsConsoleOut : Task
{
    [Output]
    public string CapturedOutput { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
