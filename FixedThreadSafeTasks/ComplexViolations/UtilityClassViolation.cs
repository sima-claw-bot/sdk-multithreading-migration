using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ComplexViolations;

/// <summary>
/// Fixed version: replaces static PathUtilities calls with instance methods that use
/// TaskEnvironment.GetAbsolutePath to resolve paths against the project directory
/// instead of the process-wide CWD.
/// </summary>
[MSBuildMultiThreadableTask]
public class UtilityClassViolation : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string AbsolutePath { get; set; } = string.Empty;

    [Output]
    public string NormalizedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Fixed: use instance methods backed by TaskEnvironment instead of static utilities.
        AbsolutePath = MakeAbsolute(InputPath);
        NormalizedPath = NormalizeSeparators(MakeAbsolute(InputPath));
        return true;
    }

    private string MakeAbsolute(string path)
    {
        if (Path.IsPathRooted(path))
            return path;

        return TaskEnvironment.GetAbsolutePath(path);
    }

    private static string NormalizeSeparators(string path)
    {
        return path.Replace('/', Path.DirectorySeparatorChar)
                   .Replace('\\', Path.DirectorySeparatorChar);
    }
}
