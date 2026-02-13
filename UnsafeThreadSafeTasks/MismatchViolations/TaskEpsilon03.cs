using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// Implements IMultiThreadableTask but null-checks TaskEnvironment and falls back to
/// Path.GetFullPath when it is null. This is unsafe because the fallback path resolves
/// relative to the process working directory, defeating the purpose of multithreading support.
/// </summary>
public class TaskEpsilon03 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = null!;

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: Falls back to unsafe Path.GetFullPath when TaskEnvironment is null
        if (TaskEnvironment != null)
        {
            Result = TaskEnvironment.GetAbsolutePath(InputPath).Value;
        }
        else
        {
            Result = Path.GetFullPath(InputPath);
        }

        return true;
    }
}
