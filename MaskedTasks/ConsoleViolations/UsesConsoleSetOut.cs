using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ConsoleViolations;

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
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
