using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: uses a single TaskEnvironment.GetAbsolutePath call instead of
/// redundant double Path.GetFullPath calls against process CWD.
/// </summary>
[MSBuildMultiThreadableTask]
public class DoubleResolvesPath : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.GetAbsolutePath(InputPath);
        return true;
    }
}
