using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ProcessViolations;

/// <summary>
/// Calls Process.GetCurrentProcess().Kill() which terminates the process.
/// This is a forbidden API in multithreaded MSBuild.
/// </summary>
public class CallsProcessKill : Task
{
    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
