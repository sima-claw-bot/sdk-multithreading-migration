using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: threads TaskEnvironment through the entire call chain so the final
/// NormalizePath method uses TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskAlpha04 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        OutputPath = PrepareOutput(InputPath);
        return true;
    }

    private string PrepareOutput(string path)
    {
        var trimmed = path.Trim();
        return BuildFullPath(trimmed);
    }

    private string BuildFullPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return NormalizePath(path);
    }

    private string NormalizePath(string path)
    {
        // Fixed: use TaskEnvironment.GetAbsolutePath instead of Path.GetFullPath.
        return TaskEnvironment.GetAbsolutePath(path);
    }
}
