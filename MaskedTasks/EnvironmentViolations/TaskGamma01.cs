using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.EnvironmentViolations;

/// <summary>
/// Reads Environment.CurrentDirectory. This is unsafe because the current directory is
/// process-global shared state that can be changed by other tasks running concurrently.
/// </summary>
public class TaskGamma01 : Task
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
