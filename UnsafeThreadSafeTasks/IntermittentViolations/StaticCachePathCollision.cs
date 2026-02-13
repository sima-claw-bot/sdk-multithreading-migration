using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class StaticCachePathCollision : Task
{
    // BUG: static cache shared by all task instances; relative-path keys collide
    // across different project directories.
    private static readonly Dictionary<string, string> PathCache = new();

    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: uses only RelativePath as the key — different ProjectDirectories that
        // share the same RelativePath will collide.
        if (PathCache.TryGetValue(RelativePath, out var cached))
        {
            ResolvedPath = cached;
            return true;
        }

        // Simulate work; widens the race window.
        Thread.Sleep(50);

        var resolved = Path.GetFullPath(Path.Combine(ProjectDirectory, RelativePath));

        // BUG: non-thread-safe Dictionary — concurrent writes can corrupt internal state.
        PathCache[RelativePath] = resolved;
        ResolvedPath = resolved;
        return true;
    }
}
