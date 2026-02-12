using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// Uses Path.GetFullPath(InputPath) to resolve a relative path. This is unsafe because
/// Path.GetFullPath resolves relative to the process working directory, not the project directory.
/// </summary>
public class UsesPathGetFullPath_AttributeOnly : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = Path.GetFullPath(InputPath);
        return true;
    }
}
