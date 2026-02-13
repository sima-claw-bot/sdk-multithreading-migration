using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// Uses a LINQ pipeline with <see cref="Enumerable.Select{TSource,TResult}"/> that calls
/// <see cref="Path.GetFullPath"/> on each element. Because LINQ evaluation is deferred, the
/// <see cref="Path.GetFullPath"/> calls execute when the pipeline is materialized, at which
/// point the process-wide CWD may have been changed by another concurrent task.
/// </summary>
public class TaskAlpha08 : Task
{
    [Required]
    public string[] RelativePaths { get; set; } = [];

    [Output]
    public string[] ResolvedPaths { get; set; } = [];

    public override bool Execute()
    {
        // BUG: Path.GetFullPath inside Select resolves against the process CWD.
        // The deferred evaluation means the CWD at enumeration time may differ
        // from the CWD at the point this pipeline was constructed.
        ResolvedPaths = RelativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => Path.GetFullPath(p))
            .Distinct()
            .ToArray();

        return true;
    }
}
