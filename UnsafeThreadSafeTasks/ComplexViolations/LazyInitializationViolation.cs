using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class LazyInitializationViolation : Task
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
