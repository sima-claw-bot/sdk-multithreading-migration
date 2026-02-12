using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ProcessViolations;

/// <summary>
/// Calls Environment.Exit() which terminates the entire process.
/// This is a forbidden API in multithreaded MSBuild because it would kill all concurrent tasks.
/// </summary>
public class CallsEnvironmentExit : Task
{
    public int ExitCode { get; set; }

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
