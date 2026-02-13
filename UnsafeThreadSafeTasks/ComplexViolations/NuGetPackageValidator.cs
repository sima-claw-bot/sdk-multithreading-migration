using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class NuGetPackageValidator : Task
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
        // BUG: Environment.GetEnvironmentVariable is process-global and can be changed
        // by other tasks concurrently via Environment.SetEnvironmentVariable.
        var globalPackagesFolder = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
                                  ?? Path.Combine(Environment.GetFolderPath(
                                      Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        var packageDir = Path.Combine(globalPackagesFolder, PackageId, PackageVersion);

        // BUG: File.Exists with a relative path resolves against the process CWD.
        // If NuspecRelativePath is relative, the check depends on whichever CWD is
        // active, not the project directory.
        if (!File.Exists(NuspecRelativePath))
        {
            Log.LogWarning("Nuspec file not found at relative path: {0}", NuspecRelativePath);
            IsValid = false;
            return true;
        }

        // BUG: XDocument.Load with a relative path resolves against the process CWD.
        var nuspec = XDocument.Load(NuspecRelativePath);

        var idElement = nuspec.Root?.Element("metadata")?.Element("id");
        if (idElement == null || !string.Equals(idElement.Value, PackageId, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning("Package ID mismatch in nuspec.");
            IsValid = false;
            return true;
        }

        // BUG: Path.GetFullPath resolves relative paths against the process CWD.
        ResolvedNuspecPath = Path.GetFullPath(NuspecRelativePath);
        IsValid = true;
        return true;
    }
}
