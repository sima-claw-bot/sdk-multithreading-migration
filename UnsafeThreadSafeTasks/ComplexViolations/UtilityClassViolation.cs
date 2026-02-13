using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ComplexViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class UtilityClassViolation : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string AbsolutePath { get; set; } = string.Empty;

    [Output]
    public string NormalizedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Looks clean — delegates to a utility class with no obvious CWD dependency.
        AbsolutePath = PathUtilities.MakeAbsolute(InputPath);
        NormalizedPath = PathUtilities.NormalizeSeparators(
            PathUtilities.MakeAbsolute(InputPath));
        return true;
    }
}

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
internal static class PathUtilities
{
    /// <summary>
    /// Converts a potentially relative path to an absolute path.
    /// </summary>
    // BUG: Path.GetFullPath resolves relative paths against the process CWD.
    public static string MakeAbsolute(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Normalizes directory separators to the platform default.
    /// </summary>
    public static string NormalizeSeparators(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar);
    }
}
