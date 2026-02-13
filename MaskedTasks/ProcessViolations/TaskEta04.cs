using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ProcessViolations;

/// <summary>
/// Spawns a process using a raw ProcessStartInfo instead of TaskEnvironment.GetProcessStartInfo().
/// This is unsafe because the ProcessStartInfo won't inherit the correct WorkingDirectory
/// or environment variables from the TaskEnvironment.
/// </summary>
public class TaskEta04 : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: creates ProcessStartInfo directly — WorkingDirectory defaults to process CWD
        var psi = new ProcessStartInfo
        {
            FileName = Command,
            Arguments = Arguments,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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
