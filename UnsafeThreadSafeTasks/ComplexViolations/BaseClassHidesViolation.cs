using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public abstract class PathResolvingTaskBase : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Resolves a relative path to an absolute path.
    /// </summary>
    // BUG: uses Path.GetFullPath which resolves against the process-wide CWD.
    protected string ResolvePath(string relativePath)
    {
        return Path.GetFullPath(relativePath);
    }
}

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class BaseClassHidesViolation : PathResolvingTaskBase
{
    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Looks clean — but delegates to the base class which uses Path.GetFullPath.
        ResolvedPath = ResolvePath(InputPath);
        return true;
    }
}
