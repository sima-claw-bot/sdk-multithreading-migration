using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Parses MSBuild project files to extract package references and project references.
/// This is unsafe because it uses <see cref="XDocument.Load(string)"/> with relative paths
/// (resolved against the process-wide CWD) and then resolves referenced project paths via
/// <see cref="Path.GetFullPath"/>. In a parallel build, the CWD may point to a different
/// project's directory, causing the wrong file to be loaded or references to resolve
/// incorrectly.
/// </summary>
public class TaskAlpha10 : Task
{
    [Required]
    public string ProjectFilePath { get; set; } = string.Empty;

    [Output]
    public string[] PackageReferences { get; set; } = [];

    [Output]
    public string[] ProjectReferences { get; set; } = [];

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }

    private static string[] ExtractPackageReferences(XDocument doc, XNamespace ns)
    {
        return doc.Descendants(ns + "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();
    }

    private string[] ExtractProjectReferences(XDocument doc, XNamespace ns)
    {
        var refs = new List<string>();

        foreach (var element in doc.Descendants(ns + "ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
                continue;

            // BUG: Path.GetFullPath resolves relative paths against the process CWD.
            // The referenced project path should be resolved relative to the directory
            // containing ProjectFilePath, not the process CWD.
            var resolvedPath = Path.GetFullPath(include);
            refs.Add(resolvedPath);
        }

        return refs.ToArray();
    }
}
