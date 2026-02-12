using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Partially migrated to TaskEnvironment: one path is resolved correctly via
/// TaskEnvironment.GetAbsolutePath, but a second path still uses the unsafe
/// Path.GetFullPath, which resolves relative to the process working directory.
/// </summary>
public class PartialMigration : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string SourcePath { get; set; } = string.Empty;

    [Required]
    public string DestinationPath { get; set; } = string.Empty;

    [Output]
    public string ResolvedSource { get; set; } = string.Empty;

    [Output]
    public string ResolvedDestination { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Correctly migrated
        ResolvedSource = TaskEnvironment.GetAbsolutePath(SourcePath);

        // BUG: missed during migration — still uses Path.GetFullPath
        ResolvedDestination = Path.GetFullPath(DestinationPath);

        return true;
    }
}
