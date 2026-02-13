using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Fixed version: resolves paths using the per-task <see cref="TaskEnvironment"/>
/// instead of a shared static <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
/// Each task instance uses <see cref="TaskEnvironment.GetAbsolutePath"/> to combine
/// the project directory with the relative path, eliminating cross-project collisions
/// and thread-safety issues from shared mutable state.
/// </summary>
[MSBuildMultiThreadableTask]
public class StaticCachePathCollision : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        ResolvedPath = TaskEnvironment.GetAbsolutePath(RelativePath);
        return true;
    }
}
