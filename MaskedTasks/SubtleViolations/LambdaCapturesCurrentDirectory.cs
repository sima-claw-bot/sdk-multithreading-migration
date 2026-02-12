using System;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.SubtleViolations;

/// <summary>
/// Captures Environment.CurrentDirectory inside a LINQ lambda. The process-global
/// current directory is read at enumeration time, making the result non-deterministic
/// under concurrent task execution.
/// </summary>
public class LambdaCapturesCurrentDirectory : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public ITaskItem[] InputFiles { get; set; } = Array.Empty<ITaskItem>();

    [Output]
    public string[] ResolvedPaths { get; set; } = Array.Empty<string>();

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
