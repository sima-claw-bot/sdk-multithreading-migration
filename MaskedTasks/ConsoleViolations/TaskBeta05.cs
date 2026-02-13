using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ConsoleViolations;

/// <summary>
/// Writes task output to Console.WriteLine instead of Log.LogMessage. This is unsafe because
/// Console output bypasses the MSBuild logging infrastructure, so the message is never captured
/// in MSBuild output or build logs.
/// </summary>
public class TaskBeta05 : Task
{
    [Required]
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
