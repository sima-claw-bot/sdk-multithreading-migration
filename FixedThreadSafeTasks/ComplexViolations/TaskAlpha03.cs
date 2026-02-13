using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: the base class uses TaskEnvironment.GetAbsolutePath instead of
/// Path.GetFullPath, resolving against the project directory rather than the process CWD.
/// </summary>
public abstract class PathResolvingTaskBase : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Resolves a relative path to an absolute path using TaskEnvironment.
    /// </summary>
    protected string ResolvePath(string relativePath)
    {
        return TaskEnvironment.GetAbsolutePath(relativePath);
    }
}

/// <summary>
/// Fixed version: inherits from the fixed PathResolvingTaskBase which uses
/// TaskEnvironment for path resolution.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskAlpha03 : PathResolvingTaskBase
{
    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        ResolvedPath = ResolvePath(InputPath);
        return true;
    }
}
