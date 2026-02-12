using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.SubtleViolations;

/// <summary>
/// Uses a static field to cache the last input value. This is unsafe because concurrent
/// task instances share the same static field, causing cross-contamination of results.
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
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
