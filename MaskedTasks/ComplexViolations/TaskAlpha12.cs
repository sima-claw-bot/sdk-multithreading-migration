using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// A static utility class that wraps <see cref="Path.GetFullPath"/> behind a clean-looking
/// helper API. The MSBuild task delegates to <see cref="PathUtilities.MakeAbsolute"/> which
/// appears benign, but internally resolves relative paths against the process-wide CWD.
/// This pattern is unsafe in parallel builds because the CWD may belong to a different
/// project than the one invoking the task.
/// </summary>
public class TaskAlpha12 : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string AbsolutePath { get; set; } = string.Empty;

    [Output]
    public string NormalizedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}

/// <summary>
/// Static utility class that hides CWD-dependent operations behind harmless-looking method
/// names. Callers appear clean, but every call to <see cref="MakeAbsolute"/> resolves
/// against the process-global CWD.
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
