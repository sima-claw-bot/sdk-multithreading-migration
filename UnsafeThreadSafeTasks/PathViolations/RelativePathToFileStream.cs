using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class RelativePathToFileStream : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        using var stream = new FileStream(InputPath, FileMode.Open);
        using var reader = new StreamReader(stream);
        Result = reader.ReadLine() ?? string.Empty;
        return true;
    }
}
