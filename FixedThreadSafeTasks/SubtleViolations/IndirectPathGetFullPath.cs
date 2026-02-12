using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: resolves paths via TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath.
/// </summary>
[MSBuildMultiThreadableTask]
public class IndirectPathGetFullPath : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = ResolvePath(InputPath);
        return true;
    }

    private string ResolvePath(string p) => TaskEnvironment.GetAbsolutePath(p);
}
