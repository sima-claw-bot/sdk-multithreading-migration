using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.ProcessViolations;

/// <summary>
/// Fixed version: uses TaskEnvironment.GetProcessStartInfo() to spawn a process
/// with the correct WorkingDirectory and environment variables.
/// </summary>
[MSBuildMultiThreadableTask]
public class UsesRawProcessStartInfo : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string Command { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        var psi = TaskEnvironment.GetProcessStartInfo();
        psi.FileName = Command;
        psi.Arguments = Arguments;
        psi.RedirectStandardOutput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;

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
