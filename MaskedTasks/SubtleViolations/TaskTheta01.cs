using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.SubtleViolations;

/// <summary>
/// Redundantly double-resolves a path through Path.GetFullPath. Both calls resolve
/// relative to the process working directory rather than TaskEnvironment, and the
/// outer call is completely redundant since the inner call already returns an absolute path.
/// </summary>
public class TaskTheta01 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

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
