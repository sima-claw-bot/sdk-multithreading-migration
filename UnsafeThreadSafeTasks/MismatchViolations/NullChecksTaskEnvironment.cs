using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
