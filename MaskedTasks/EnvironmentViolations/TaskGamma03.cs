using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.EnvironmentViolations;

/// <summary>
/// Reads an environment variable via Environment.GetEnvironmentVariable. This is unsafe
/// because environment variables are process-global shared state that can be modified
/// concurrently by other tasks.
/// </summary>
public class TaskGamma03 : Task
{
    [Required]
    public string VariableName { get; set; } = string.Empty;

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
