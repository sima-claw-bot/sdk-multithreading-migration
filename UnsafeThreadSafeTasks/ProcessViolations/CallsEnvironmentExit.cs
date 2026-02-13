using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
