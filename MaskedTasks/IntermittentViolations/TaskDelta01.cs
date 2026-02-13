using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Changes <see cref="Environment.CurrentDirectory"/> (process-global) to the project
/// directory, then resolves a relative path against it. When multiple task instances run
/// concurrently on different threads, each one mutates the same global CWD, so the
/// resolved path may point to a completely wrong directory.
/// </summary>
public class TaskDelta01 : Task
{
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
