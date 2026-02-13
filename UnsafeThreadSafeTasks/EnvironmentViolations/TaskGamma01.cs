using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Reads Environment.CurrentDirectory. This is unsafe because the current directory is
/// process-global shared state that can be changed by other tasks running concurrently.
/// </summary>
public class TaskGamma01 : Task
{
    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = Environment.CurrentDirectory;
        return true;
    }
}
