using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.MismatchViolations;

/// <summary>
/// Fixed version: implements IMultiThreadableTask and resolves paths via TaskEnvironment
/// before calling File.Exists.
/// </summary>
[MSBuildMultiThreadableTask]
public class AttributeOnlyWithForbiddenApis : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        string absolutePath = TaskEnvironment.GetAbsolutePath(InputPath);
        Result = File.Exists(absolutePath).ToString();
        return true;
    }
}
