using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.ConsoleViolations;

/// <summary>
/// Calls Console.ReadLine which blocks waiting for stdin input. This is unsafe because it
/// can hang the entire build indefinitely. A BlockingMode guard and a 2-second cancellation
/// timeout are used to detect blocking without actually hanging the build.
/// </summary>
public class UsesConsoleReadLine : Task
{
    public bool BlockingMode { get; set; }

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        if (!BlockingMode)
        {
            Result = "SKIPPED";
            return true;
        }

        Result = "BLOCKED";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            // BUG: Console.ReadLine blocks on stdin — will hang MSBuild builds
            var readTask = System.Threading.Tasks.Task.Run(() => Console.ReadLine(), cts.Token);
            readTask.Wait(cts.Token);
            Result = readTask.Result ?? "NULL";
        }
        catch (OperationCanceledException)
        {
            // Timeout reached — confirms blocking behavior
            Log.LogWarning("Console.ReadLine blocked for 2 seconds; build would hang.");
        }

        return true;
    }
}
