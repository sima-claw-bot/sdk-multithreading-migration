using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.GetAbsolutePath to resolve the nuspec relative path
/// against the project directory instead of the process CWD, and reads the NUGET_PACKAGES
/// environment variable via TaskEnvironment.GetEnvironmentVariable instead of the
/// process-global Environment.GetEnvironmentVariable.
/// </summary>
[MSBuildMultiThreadableTask]
public class NuGetPackageValidator : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

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
        // Fixed: read NUGET_PACKAGES from TaskEnvironment instead of process-global env.
        var globalPackagesFolder = TaskEnvironment.GetEnvironmentVariable("NUGET_PACKAGES")
                                  ?? Path.Combine(Environment.GetFolderPath(
                                      Environment.SpecialFolder.UserProfile), ".nuget", "packages");

        var packageDir = Path.Combine(globalPackagesFolder, PackageId, PackageVersion);

        // Fixed: resolve NuspecRelativePath against the project directory via TaskEnvironment.
        string absoluteNuspecPath = TaskEnvironment.GetAbsolutePath(NuspecRelativePath);

        if (!File.Exists(absoluteNuspecPath))
        {
            Log.LogWarning("Nuspec file not found at path: {0}", absoluteNuspecPath);
            IsValid = false;
            return true;
        }

        // Fixed: load using the absolute path resolved via TaskEnvironment.
        var nuspec = XDocument.Load(absoluteNuspecPath);

        var idElement = nuspec.Root?.Element("metadata")?.Element("id");
        if (idElement == null || !string.Equals(idElement.Value, PackageId, StringComparison.OrdinalIgnoreCase))
        {
            Log.LogWarning("Package ID mismatch in nuspec.");
            IsValid = false;
            return true;
        }

        // Fixed: path is already absolute from TaskEnvironment.GetAbsolutePath.
        ResolvedNuspecPath = absoluteNuspecPath;
        IsValid = true;
        return true;
    }
}
