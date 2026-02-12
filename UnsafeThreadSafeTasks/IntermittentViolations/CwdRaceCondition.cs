using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Changes <see cref="Environment.CurrentDirectory"/> (process-global) to the project
/// directory, then resolves a relative path against it. When multiple task instances run
/// concurrently on different threads, each one mutates the same global CWD, so the
/// resolved path may point to a completely wrong directory.
/// </summary>
public class CwdRaceCondition : Task
{
    [Required]
    public string ProjectDirectory { get; set; } = string.Empty;

    [Required]
    public string RelativePath { get; set; } = string.Empty;

    [Output]
    public string ResolvedPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: Environment.CurrentDirectory is process-global; another thread can
        // change it between SetCurrentDirectory and GetFullPath.
        Environment.CurrentDirectory = ProjectDirectory;

        // Simulate work; widens the race window so another thread can change CWD.
        Thread.Sleep(50);

        ResolvedPath = Path.GetFullPath(RelativePath);
        return true;
    }
}
