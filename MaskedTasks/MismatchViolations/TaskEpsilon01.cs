using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.MismatchViolations;

/// <summary>
/// Has [MSBuildMultiThreadableTask] attribute but uses File.Exists with a relative path
/// without implementing IMultiThreadableTask. This is unsafe because File.Exists resolves
/// relative paths against the process working directory, and without IMultiThreadableTask
/// the task has no access to TaskEnvironment for proper path resolution.
/// </summary>
[MSBuildMultiThreadableTask]
public class TaskEpsilon01 : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
