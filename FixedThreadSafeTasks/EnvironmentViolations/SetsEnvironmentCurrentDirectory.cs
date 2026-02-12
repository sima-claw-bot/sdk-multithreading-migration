using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.ProjectDirectory to resolve relative file paths
/// instead of setting the process-global Environment.CurrentDirectory.
/// </summary>
[MSBuildMultiThreadableTask]
public class SetsEnvironmentCurrentDirectory : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string NewDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativeFilePath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        TaskEnvironment.ProjectDirectory = NewDirectory;
        string absolutePath = TaskEnvironment.GetAbsolutePath(RelativeFilePath);
        Result = File.ReadAllText(absolutePath);
        return true;
    }
}
