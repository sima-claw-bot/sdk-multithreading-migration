using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Fixed version: each task instance writes to a unique temporary file path
/// (incorporating a GUID) so that concurrent instances never clobber each other's data.
/// </summary>
[MSBuildMultiThreadableTask]
public class SharedTempFileConflict : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Content { get; set; } = string.Empty;

    [Output]
    public string ReadBack { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Use a unique temp file per task instance to avoid conflicts.
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"FixedThreadSafeTasks_{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(tempFilePath, Content);

            Thread.Sleep(50);

            ReadBack = File.ReadAllText(tempFilePath);
            return true;
        }
        finally
        {
            try { File.Delete(tempFilePath); } catch { }
        }
    }
}
