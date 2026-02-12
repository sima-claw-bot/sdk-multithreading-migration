using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.PathViolations;

/// <summary>
/// Fixed version: absolutizes the path via TaskEnvironment before calling Directory.Exists.
/// </summary>
public class RelativePathToDirectoryExists : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        string absolutePath = TaskEnvironment.GetAbsolutePath(InputPath);
        Result = Directory.Exists(absolutePath).ToString();
        return true;
    }
}
