using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// The base class <see cref="PathResolvingTaskBase"/> uses <see cref="Path.GetFullPath"/>
/// to resolve paths, but the derived class <see cref="BaseClassHidesViolation"/> appears
/// clean — it only calls the base method. This pattern hides the CWD-dependent violation
/// behind an inheritance boundary, making it harder to detect via simple static analysis.
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
/// Derived class that appears thread-safe — it has no direct CWD or
/// <see cref="Path.GetFullPath"/> usage. The violation is hidden in
/// <see cref="PathResolvingTaskBase.ResolvePath"/>.
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
