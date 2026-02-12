using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: uses an instance field instead of a static field, so each task
/// instance has its own results list and there are no cross-instance race conditions.
/// </summary>
[MSBuildMultiThreadableTask]
public class SharedMutableStaticField : Task, IMultiThreadableTask
{
    private readonly List<string> _allResults = new();

    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputValue { get; set; } = string.Empty;

    [Output]
    public string[] AllResults { get; set; } = System.Array.Empty<string>();

    public override bool Execute()
    {
        _allResults.Add(InputValue);
        AllResults = _allResults.ToArray();
        return true;
    }
}
