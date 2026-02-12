using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Hides the unsafe Path.GetFullPath call behind a private helper method.
/// The violation is indirect — Execute delegates to ResolvePath, which still
/// resolves relative to the process working directory instead of TaskEnvironment.
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
