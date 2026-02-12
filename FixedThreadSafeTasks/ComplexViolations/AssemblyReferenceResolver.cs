using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.GetAbsolutePath to resolve assembly paths against
/// the project directory instead of the process CWD, and uses an instance-level cache
/// instead of a static cache to prevent cross-task contamination.
/// </summary>
[MSBuildMultiThreadableTask]
public class AssemblyReferenceResolver : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    // Fixed: instance cache instead of static — no sharing across task instances.
    private readonly Dictionary<string, string> ResolvedAssemblyCache = new();

    [Required]
    public string[] AssemblyNames { get; set; } = [];

    [Required]
    public string ReferencePath { get; set; } = string.Empty;

    [Output]
    public string[] ResolvedPaths { get; set; } = [];

    public override bool Execute()
    {
        var results = new List<string>();

        foreach (var assemblyName in AssemblyNames)
        {
            if (ResolvedAssemblyCache.TryGetValue(assemblyName, out var cached))
            {
                results.Add(cached);
                continue;
            }

            var resolved = ResolveAssembly(assemblyName);
            ResolvedAssemblyCache[assemblyName] = resolved;
            results.Add(resolved);
        }

        ResolvedPaths = results.ToArray();
        return true;
    }

    private string ResolveAssembly(string assemblyName)
    {
        // Fixed: resolve ReferencePath against the project directory via TaskEnvironment.
        string absoluteRefPath = TaskEnvironment.GetAbsolutePath(ReferencePath);
        var fullPath = Path.Combine(absoluteRefPath, assemblyName + ".dll");

        if (File.Exists(fullPath))
        {
            return Path.GetFullPath(fullPath);
        }

        return string.Empty;
    }
}
