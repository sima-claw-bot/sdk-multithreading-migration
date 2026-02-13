using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
