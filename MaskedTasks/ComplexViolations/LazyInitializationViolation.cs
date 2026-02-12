using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Uses a static <see cref="Lazy{T}"/> whose factory calls <see cref="Path.GetFullPath"/>.
/// The <see cref="Lazy{T}"/> ensures the factory runs exactly once, but whichever task
/// triggers initialization captures the CWD that happens to be active at that moment.
/// All subsequent tasks receive a path resolved against the wrong project directory.
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
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
