using System.Collections.Concurrent;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Uses a static <see cref="ConcurrentDictionary{TKey, TValue}"/> to cache resolved paths,
/// but the cache keys are relative paths that get resolved via <see cref="Path.GetFullPath"/>
/// against the process-wide CWD. Two tasks in different project directories that cache the
/// same relative key will silently share incorrect results because the resolved value depends
/// on whichever CWD was active when the entry was first inserted.
/// </summary>
public class DictionaryCacheViolation : Task
{
    // BUG: static cache persists across task instances — entries resolved under one CWD
    // are served to tasks running under a different CWD.
    private static readonly ConcurrentDictionary<string, string> PathCache = new();

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
