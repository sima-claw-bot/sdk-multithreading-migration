using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.EnvironmentViolations;

/// <summary>
/// Sets an environment variable via Environment.SetEnvironmentVariable and then reads it back.
/// This is unsafe because environment variables are process-global shared state and modifying
/// them can affect other tasks running concurrently.
/// </summary>
public class TaskGamma04 : Task
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

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
