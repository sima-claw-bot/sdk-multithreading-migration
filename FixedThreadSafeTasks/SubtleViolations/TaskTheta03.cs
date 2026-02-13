using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.SubtleViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.ProjectDirectory instead of Environment.CurrentDirectory
/// in the LINQ lambda, ensuring thread-safe path resolution.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskTheta03 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public ITaskItem[] InputFiles { get; set; } = Array.Empty<ITaskItem>();

    [Output]
    public string[] ResolvedPaths { get; set; } = Array.Empty<string>();

    public override bool Execute()
    {
        ResolvedPaths = InputFiles
            .Select(item => System.IO.Path.Combine(TaskEnvironment.ProjectDirectory, item.ItemSpec))
            .ToArray();

        return true;
    }
}
