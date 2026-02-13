using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
