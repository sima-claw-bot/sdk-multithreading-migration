using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// Opens a FileStream with a relative path. This is unsafe because FileStream resolves
/// relative paths against the process working directory, not the project directory.
/// </summary>
public class TaskZeta03 : Task
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
