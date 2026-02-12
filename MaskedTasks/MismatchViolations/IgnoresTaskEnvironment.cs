using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.MismatchViolations;

/// <summary>
/// Implements IMultiThreadableTask with TaskEnvironment property but never reads from it.
/// Instead it uses Path.GetFullPath directly, ignoring the thread-safe environment provided
/// by MSBuild. This is unsafe because the task opts in to multithreading but does not use
/// the facilities that make it safe.
/// </summary>
public class IgnoresTaskEnvironment : Task, IMultiThreadableTask
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
