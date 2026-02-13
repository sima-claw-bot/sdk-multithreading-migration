using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class SharedMutableStaticField : Task
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
