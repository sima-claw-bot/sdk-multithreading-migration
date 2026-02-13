using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace MaskedTasks.ConsoleViolations;

/// <summary>
/// Calls Console.ReadLine which blocks waiting for stdin input. This is unsafe because it
/// can hang the entire build indefinitely. A BlockingMode guard and a 2-second cancellation
/// timeout are used to detect blocking without actually hanging the build.
/// </summary>
public class TaskBeta03 : Task
{
    public bool BlockingMode { get; set; }

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
