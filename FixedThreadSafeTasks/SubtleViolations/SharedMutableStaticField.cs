using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: uses an instance field instead of a static field so each task
/// instance has its own isolated storage. No cross-contamination between threads.
/// </summary>
[MSBuildMultiThreadableTask]
public class SharedMutableStaticField : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    private string _lastValue = string.Empty;

    [Required]
    public string InputValue { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        _lastValue = InputValue;
        Thread.Sleep(50);
        Result = _lastValue;
        return true;
    }
}
