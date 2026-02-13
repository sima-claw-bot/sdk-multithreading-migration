using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// Uses a static <see cref="Lazy{T}"/> whose factory calls <see cref="Path.GetFullPath"/>.
/// The <see cref="Lazy{T}"/> ensures the factory runs exactly once, but whichever task
/// triggers initialization captures the CWD that happens to be active at that moment.
/// All subsequent tasks receive a path resolved against the wrong project directory.
/// </summary>
public class TaskAlpha07 : Task
{
    // BUG: Lazy<T> factory captures the CWD at first-access time. All later accesses
    // return the same stale value regardless of the actual project directory.
    private static readonly Lazy<string> CachedToolPath =
        new(() => Path.GetFullPath("tools"));

    [Required]
    public string ToolName { get; set; } = string.Empty;

    [Output]
    public string ResolvedToolPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: CachedToolPath.Value was resolved against whatever CWD was active when
        // the Lazy<T> was first accessed — not necessarily this task's project directory.
        ResolvedToolPath = Path.Combine(CachedToolPath.Value, ToolName);
        return true;
    }
}
