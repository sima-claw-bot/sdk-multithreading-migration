using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class DeepCallChainPathResolve : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Looks clean — no CWD or Path usage here.
        OutputPath = PrepareOutput(InputPath);
        return true;
    }

    private string PrepareOutput(string path)
    {
        // Level 2: still looks harmless — just delegates further.
        var trimmed = path.Trim();
        return BuildFullPath(trimmed);
    }

    private string BuildFullPath(string path)
    {
        // Level 3: delegates one more time.
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return NormalizePath(path);
    }

    private string NormalizePath(string path)
    {
        // BUG: Path.GetFullPath resolves relative paths against the process CWD.
        // This is 3 levels deep from Execute(), making it hard to spot.
        return Path.GetFullPath(path);
    }
}
