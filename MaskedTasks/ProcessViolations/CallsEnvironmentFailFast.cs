using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ProcessViolations;

/// <summary>
/// Calls Environment.FailFast() which immediately terminates the process without cleanup.
/// This is a forbidden API in multithreaded MSBuild.
/// </summary>
public class CallsEnvironmentFailFast : Task
{
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
