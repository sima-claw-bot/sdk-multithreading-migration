using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class RelativePathToFileExists : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = File.Exists(InputPath).ToString();
        return true;
    }
}
