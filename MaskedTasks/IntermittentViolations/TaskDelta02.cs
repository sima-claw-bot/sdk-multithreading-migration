using System;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Reads an environment variable, does some work, then reads the same variable again
/// and expects it to be unchanged. Because environment variables are process-global,
/// another thread can call <see cref="Environment.SetEnvironmentVariable"/> between the
/// two reads, causing a classic Time-Of-Check-to-Time-Of-Use (TOCTOU) bug.
/// </summary>
public class TaskDelta02 : Task
{
    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Output]
    public string InitialValue { get; set; } = string.Empty;

    [Output]
    public string FinalValue { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
