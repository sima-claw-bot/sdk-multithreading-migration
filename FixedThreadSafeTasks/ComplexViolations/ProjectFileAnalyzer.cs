using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.GetAbsolutePath to resolve the project file path
/// and project references against the project directory instead of the process CWD.
/// </summary>
[MSBuildMultiThreadableTask]
public class ProjectFileAnalyzer : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string ProjectFilePath { get; set; } = string.Empty;

    [Output]
    public string[] PackageReferences { get; set; } = [];

    [Output]
    public string[] ProjectReferences { get; set; } = [];

    public override bool Execute()
    {
        // Fixed: resolve ProjectFilePath against the project directory via TaskEnvironment.
        string absoluteProjectFilePath = TaskEnvironment.GetAbsolutePath(ProjectFilePath);

        if (!File.Exists(absoluteProjectFilePath))
        {
            Log.LogError("Project file not found: {0}", absoluteProjectFilePath);
            return false;
        }

        var doc = XDocument.Load(absoluteProjectFilePath);
        var ns = doc.Root?.Name.Namespace ?? XNamespace.None;

        PackageReferences = ExtractPackageReferences(doc, ns);
        ProjectReferences = ExtractProjectReferences(doc, ns, absoluteProjectFilePath);

        return true;
    }

    private static string[] ExtractPackageReferences(XDocument doc, XNamespace ns)
    {
        return doc.Descendants(ns + "PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToArray();
    }

    private string[] ExtractProjectReferences(XDocument doc, XNamespace ns, string absoluteProjectFilePath)
    {
        var refs = new List<string>();
        var projectDir = Path.GetDirectoryName(absoluteProjectFilePath) ?? string.Empty;

        foreach (var element in doc.Descendants(ns + "ProjectReference"))
        {
            var include = element.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
                continue;

            // Fixed: resolve the project reference relative to the directory containing
            // the project file, not the process CWD.
            var resolvedPath = Path.GetFullPath(Path.Combine(projectDir, include));
            refs.Add(resolvedPath);
        }

        return refs.ToArray();
    }
}
