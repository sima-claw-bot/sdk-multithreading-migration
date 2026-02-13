using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ConsoleViolations;

/// <summary>
/// Writes output via Console.WriteLine. This is unsafe because Console.Out is
/// process-global shared state — concurrent tasks writing to Console.Out will
/// produce interleaved output.
/// </summary>
public class TaskBeta07 : Task
{
    [Required]
    public string Message { get; set; } = string.Empty;

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
