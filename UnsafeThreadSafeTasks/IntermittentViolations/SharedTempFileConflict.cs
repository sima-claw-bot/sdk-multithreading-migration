using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class SharedTempFileConflict : Task
{
    // BUG: hardcoded path shared by every instance — concurrent tasks clobber each other.
    private static readonly string TempFilePath =
        Path.Combine(Path.GetTempPath(), "UnsafeThreadSafeTasks_shared.tmp");

    [Required]
    public string Content { get; set; } = string.Empty;

    [Output]
    public string ReadBack { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: write then read with a gap — another instance can overwrite in between.
        File.WriteAllText(TempFilePath, Content);

        // Simulate work; widens the race window so another instance can overwrite the file.
        Thread.Sleep(50);

        ReadBack = File.ReadAllText(TempFilePath);
        return true;
    }
}
