using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Uses a LINQ pipeline with <see cref="Enumerable.Select{TSource,TResult}"/> that calls
/// <see cref="Path.GetFullPath"/> on each element. Because LINQ evaluation is deferred, the
/// <see cref="Path.GetFullPath"/> calls execute when the pipeline is materialized, at which
/// point the process-wide CWD may have been changed by another concurrent task.
/// </summary>
public class LinqPipelineViolation : Task
{
    [Required]
    public string[] RelativePaths { get; set; } = [];

    [Output]
    public string[] ResolvedPaths { get; set; } = [];

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
