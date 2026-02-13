using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Maintains a static <see cref="Dictionary{TKey,TValue}"/> that caches resolved paths
/// keyed by relative path. When different project directories supply the same relative
/// path (e.g., "src\Program.cs"), the cache returns the result from whichever project
/// populated it first, causing cross-project path collisions.
/// </summary>
public class TaskDelta08 : Task
{
    // BUG: static cache shared by all task instances; relative-path keys collide
    // across different project directories.
    private static readonly Dictionary<string, string> PathCache = new();

    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
