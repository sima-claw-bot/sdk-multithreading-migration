using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Spawns a process whose ProcessStartInfo inherits WorkingDirectory from the process CWD.
/// If another thread changes Environment.CurrentDirectory between the time the PSI is
/// constructed and the process is started, the child process inherits the wrong directory.
/// </summary>
public class TaskDelta05 : Task
{
    [Required]
    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

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
