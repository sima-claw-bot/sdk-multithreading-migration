#nullable enable
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: uses an instance event instead of a static event, and resolves paths
/// via <see cref="TaskEnvironment.GetAbsolutePath"/> instead of <see cref="Path.GetFullPath"/>.
/// No handler accumulation across task instances and no CWD dependency.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskAlpha06 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        ResolvedPath = TaskEnvironment.GetAbsolutePath(RelativePath);
        return true;
    }
}
