using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// Calls Environment.Exit() which terminates the entire process.
/// This is a forbidden API in multithreaded MSBuild because it would kill all concurrent tasks.
/// </summary>
public class CallsEnvironmentExit : Task
{
    public int ExitCode { get; set; }

    public override bool Execute()
    {
        Environment.Exit(ExitCode);
        return true; // never reached
    }
}
