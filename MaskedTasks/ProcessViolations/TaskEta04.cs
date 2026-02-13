using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ProcessViolations;

/// <summary>
/// Spawns a process using a raw ProcessStartInfo instead of TaskEnvironment.GetProcessStartInfo().
/// This is unsafe because the ProcessStartInfo won't inherit the correct WorkingDirectory
/// or environment variables from the TaskEnvironment.
/// </summary>
public class TaskEta04 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

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
