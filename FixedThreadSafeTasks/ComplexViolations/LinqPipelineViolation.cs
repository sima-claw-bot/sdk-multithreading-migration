using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: uses <see cref="TaskEnvironment.GetAbsolutePath"/> inside the LINQ pipeline
/// instead of <see cref="System.IO.Path.GetFullPath"/>. Resolves against the task's project
/// directory rather than the process-wide CWD.
/// </summary>
[MSBuildMultiThreadableTask]
public class LinqPipelineViolation : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string[] RelativePaths { get; set; } = [];

    [Output]
    public string[] ResolvedPaths { get; set; } = [];

    public override bool Execute()
    {
        ResolvedPaths = RelativePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => (string)TaskEnvironment.GetAbsolutePath(p))
            .Distinct()
            .ToArray();

        return true;
    }
}
