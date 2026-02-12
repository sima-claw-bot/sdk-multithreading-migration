using System.Collections.Generic;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Uses a static dictionary as a registry-style key-value store that is shared across all
/// task instances. Concurrent tasks can overwrite each other's entries, and a task may read
/// a value that was written by a completely different task instance.
/// </summary>
public class RegistryStyleGlobalState : Task
{
    // BUG: static mutable dictionary shared by all task instances in the process.
    private static readonly Dictionary<string, string> Registry = new();

    [Required]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

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
