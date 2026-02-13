using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class DoubleResolvesPath : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: double Path.GetFullPath — both resolve against process CWD, outer call is redundant
        Result = Path.GetFullPath(Path.GetFullPath(InputPath));
        return true;
    }
}
