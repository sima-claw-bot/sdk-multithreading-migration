using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.SubtleViolations;

/// <summary>
/// Hides the unsafe Path.GetFullPath call behind a private helper method.
/// The violation is indirect — Execute delegates to ResolvePath, which still
/// resolves relative to the process working directory instead of TaskEnvironment.
/// </summary>
public class TaskTheta02 : Task, IMultiThreadableTask
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

    // BUG: resolves against process CWD, not TaskEnvironment.ProjectDirectory
    private string ResolvePath(string p) => Path.GetFullPath(p);
}
