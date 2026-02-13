using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Spawns a process whose ProcessStartInfo inherits WorkingDirectory from the process CWD.
/// If another thread changes Environment.CurrentDirectory between the time the PSI is
/// constructed and the process is started, the child process inherits the wrong directory.
/// </summary>
public class TaskDelta05 : Task
{
    [Required]
    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: WorkingDirectory defaults to Environment.CurrentDirectory (process-global),
        // which another thread may change at any moment.
        var psi = new ProcessStartInfo
        {
            FileName = Command,
            Arguments = Arguments,
            WorkingDirectory = Environment.CurrentDirectory,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Simulate work; widens the race window so another thread can change CWD.
        Thread.Sleep(50);

        using var process = Process.Start(psi);
        if (process == null)
        {
            Log.LogError("Failed to start process: {0}", Command);
            return false;
        }

        Result = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return true;
    }
}
