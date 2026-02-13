using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Queues work items to the <see cref="ThreadPool"/> that resolve file paths using the
/// current working directory. This is unsafe because the thread-pool callback executes at
/// an indeterminate time on a pool thread, and by then the process-wide CWD may have been
/// changed by another task. The resolved paths therefore depend on timing rather than on
/// the originating project's directory.
/// </summary>
public class TaskAlpha11 : Task
{
    [Required]
    public string RelativeFilePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedFilePath { get; set; } = string.Empty;

    [Output]
    public bool FileFound { get; set; }

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
