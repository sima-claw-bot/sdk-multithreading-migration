using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Resolves assembly reference paths by checking <see cref="File.Exists"/> with relative
/// paths and caching results in a static dictionary. This is unsafe because
/// <see cref="File.Exists"/> resolves relative paths against the process-wide CWD, so
/// concurrent tasks with different project directories get incorrect results. The static
/// cache compounds the problem by serving stale entries across projects.
/// </summary>
public class TaskAlpha01 : Task
{
    // BUG: static cache shared across all task instances — assemblies resolved for one
    // project directory are incorrectly reused for another.
    private static readonly Dictionary<string, string> ResolvedAssemblyCache = new();

    [Required]
    public string[] AssemblyNames { get; set; } = [];

    [Required]
    public string ReferencePath { get; set; } = string.Empty;

    [Output]
    public string[] ResolvedPaths { get; set; } = [];

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }

    private string ResolveAssembly(string assemblyName)
    {
        // BUG: File.Exists with a relative path resolves against the process CWD,
        // not the project directory. Different projects will see different results
        // depending on which CWD happens to be active.
        var relativePath = Path.Combine(ReferencePath, assemblyName + ".dll");

        if (File.Exists(relativePath))
        {
            return Path.GetFullPath(relativePath);
        }

        return string.Empty;
    }
}
