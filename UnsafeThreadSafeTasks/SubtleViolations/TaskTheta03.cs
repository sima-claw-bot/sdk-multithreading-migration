using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.SubtleViolations;

/// <summary>
/// Captures Environment.CurrentDirectory inside a LINQ lambda. The process-global
/// current directory is read at enumeration time, making the result non-deterministic
/// under concurrent task execution.
/// </summary>
public class TaskTheta03 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public ITaskItem[] InputFiles { get; set; } = Array.Empty<ITaskItem>();

    [Output]
    public string[] ResolvedPaths { get; set; } = Array.Empty<string>();

    public override bool Execute()
    {
        // BUG: captures Environment.CurrentDirectory in lambda — process-global shared state
        ResolvedPaths = InputFiles
            .Select(item => System.IO.Path.Combine(Environment.CurrentDirectory, item.ItemSpec))
            .ToArray();

        return true;
    }
}
