using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.MismatchViolations;

/// <summary>
/// Implements IMultiThreadableTask but null-checks TaskEnvironment and falls back to
/// Path.GetFullPath when it is null. This is unsafe because the fallback path resolves
/// relative to the process working directory, defeating the purpose of multithreading support.
/// </summary>
public class NullChecksTaskEnvironment : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = null!;

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
