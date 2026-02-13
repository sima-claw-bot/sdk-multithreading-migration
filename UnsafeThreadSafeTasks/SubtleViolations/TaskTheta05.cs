using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Uses a static field to cache the last input value. This is unsafe because concurrent
/// task instances share the same static field, causing cross-contamination of results.
/// </summary>
public class TaskTheta05 : Task
{
    private static string _lastValue = string.Empty;

    [Required]
    public string InputValue { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        _lastValue = InputValue;
        // Simulate work; widens the race window so the other thread can overwrite _lastValue.
        Thread.Sleep(50);
        Result = _lastValue;
        return true;
    }
}
