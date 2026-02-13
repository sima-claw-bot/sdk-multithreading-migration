using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Multiple task instances all write intermediate results to the same hardcoded temporary
/// file path. When tasks run concurrently, they overwrite each other's data, and the file
/// read back may contain content from a different task instance.
/// </summary>
public class TaskDelta07 : Task
{
    // BUG: hardcoded path shared by every instance — concurrent tasks clobber each other.
    private static readonly string TempFilePath =
        Path.Combine(Path.GetTempPath(), "UnsafeThreadSafeTasks_shared.tmp");

    [Required]
    public string Content { get; set; } = string.Empty;

    [Output]
    public string ReadBack { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
