using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
[MSBuildMultiThreadableTask]
public class AttributeOnlyWithForbiddenApis : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: Uses File.Exists with a relative path without IMultiThreadableTask
        Result = File.Exists(InputPath).ToString();
        return true;
    }
}
