using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
