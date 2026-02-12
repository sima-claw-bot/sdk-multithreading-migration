using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Sets Environment.CurrentDirectory and then reads a relative file. This is unsafe because
/// the current directory is process-global shared state and changing it affects all relative
/// path resolution across concurrently running tasks.
/// </summary>
public class SetsEnvironmentCurrentDirectory : Task
{
    [Required]
    public string NewDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativeFilePath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Environment.CurrentDirectory = NewDirectory;
        Result = File.ReadAllText(RelativeFilePath);
        return true;
    }
}
