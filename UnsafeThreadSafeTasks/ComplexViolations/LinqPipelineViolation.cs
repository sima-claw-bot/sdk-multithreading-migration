using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class LinqPipelineViolation : Task
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
