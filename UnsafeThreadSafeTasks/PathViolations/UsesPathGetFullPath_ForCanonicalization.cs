using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// Uses Path.GetFullPath to canonicalize a path with ".." segments. This is unsafe because
/// Path.GetFullPath resolves relative to the process working directory, not the project directory.
/// </summary>
public class UsesPathGetFullPath_ForCanonicalization : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Canonicalize the path by resolving ".." segments
        Result = Path.GetFullPath(InputPath);
        return true;
    }
}
