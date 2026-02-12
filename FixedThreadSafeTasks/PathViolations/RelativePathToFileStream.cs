using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.PathViolations;

/// <summary>
/// Fixed version: resolves relative paths via TaskEnvironment before opening FileStream.
/// </summary>
[MSBuildMultiThreadableTask]
public class RelativePathToFileStream : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        string absolutePath = TaskEnvironment.GetAbsolutePath(InputPath);
        using var stream = new FileStream(absolutePath, FileMode.Open);
        using var reader = new StreamReader(stream);
        Result = reader.ReadLine() ?? string.Empty;
        return true;
    }
}
