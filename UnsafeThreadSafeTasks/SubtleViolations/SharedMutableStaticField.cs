using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Uses a static mutable List&lt;string&gt; to accumulate results across task instances.
/// This is unsafe because multiple task instances running concurrently share the same
/// static field, causing race conditions and corrupted results.
/// </summary>
public class SharedMutableStaticField : Task, IMultiThreadableTask
{
    // BUG: static mutable state shared across all instances — not thread-safe
    private static readonly List<string> s_allResults = new();

    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputValue { get; set; } = string.Empty;

    [Output]
    public string[] AllResults { get; set; } = System.Array.Empty<string>();

    public override bool Execute()
    {
        s_allResults.Add(InputValue);
        AllResults = s_allResults.ToArray();
        return true;
    }
}
