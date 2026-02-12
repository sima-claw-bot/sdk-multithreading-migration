using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.MismatchViolations;

/// <summary>
/// Has [MSBuildMultiThreadableTask] attribute but uses File.Exists with a relative path
/// without implementing IMultiThreadableTask. This is unsafe because File.Exists resolves
/// relative paths against the process working directory, and without IMultiThreadableTask
/// the task has no access to TaskEnvironment for proper path resolution.
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
