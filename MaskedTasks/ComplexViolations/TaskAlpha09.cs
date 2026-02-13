using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Validates NuGet packages by checking that referenced package files exist on disk and
/// that the package manifest is well-formed. This is unsafe because it uses
/// <see cref="File.Exists"/> with relative paths (resolved against the process-wide CWD),
/// reads environment variables via <see cref="Environment.GetEnvironmentVariable"/> to
/// locate the global packages folder, and loads XML documents with
/// <see cref="XDocument.Load(string)"/> using relative paths. In a multi-threaded build,
/// the CWD may belong to a different project, causing incorrect file lookups.
/// </summary>
public class TaskAlpha09 : Task
{
    [Required]
    public string PackageId { get; set; } = string.Empty;

    [Required]
    public string PackageVersion { get; set; } = string.Empty;

    [Required]
    public string NuspecRelativePath { get; set; } = string.Empty;

    [Output]
    public bool IsValid { get; set; }

    [Output]
    public string ResolvedNuspecPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
