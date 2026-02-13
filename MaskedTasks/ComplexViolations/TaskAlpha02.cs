using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Captures the current working directory inside an async delegate. The delegate may
/// execute later on a different thread, by which time another task may have changed the
/// process-wide CWD. This is unsafe because the resolved path depends on whichever CWD
/// is active at delegate-execution time, not at capture time.
/// </summary>
public class TaskAlpha02 : Task
{
    [Required]
    public string RelativePath { get; set; } = string.Empty;

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
