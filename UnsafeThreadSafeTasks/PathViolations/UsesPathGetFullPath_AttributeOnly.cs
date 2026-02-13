using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
