using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: computes the tool path fresh per execution via
/// <see cref="TaskEnvironment.GetAbsolutePath"/> instead of caching in a static
/// <see cref="System.Lazy{T}"/>. Each task resolves against its own project directory.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskAlpha07 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string ToolName { get; set; } = string.Empty;

    [Output]
    public string ResolvedToolPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        string toolsDir = TaskEnvironment.GetAbsolutePath("tools");
        ResolvedToolPath = Path.Combine(toolsDir, ToolName);
        return true;
    }
}
