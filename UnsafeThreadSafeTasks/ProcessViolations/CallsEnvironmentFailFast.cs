using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class CallsEnvironmentFailFast : Task
{
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Environment.FailFast(Message);
        return true; // never reached
    }
}
