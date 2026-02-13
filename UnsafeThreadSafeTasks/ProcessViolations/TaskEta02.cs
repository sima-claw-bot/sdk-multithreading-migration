using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// Calls Environment.FailFast() which immediately terminates the process without cleanup.
/// This is a forbidden API in multithreaded MSBuild.
/// </summary>
public class TaskEta02 : Task
{
    public string Message { get; set; } = string.Empty;

    public override bool Execute()
    {
        Environment.FailFast(Message);
        return true; // never reached
    }
}
