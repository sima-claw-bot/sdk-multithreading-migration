using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// Calls File.Exists with a relative path. This is unsafe because File.Exists resolves
/// relative paths against the process working directory, not the project directory.
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
