using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
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

    // BUG: resolves against process CWD, not TaskEnvironment.ProjectDirectory
    private string ResolvePath(string p) => Path.GetFullPath(p);
}
