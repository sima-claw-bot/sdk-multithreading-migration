using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class ReadsEnvironmentCurrentDirectory : Task
{
    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = Environment.CurrentDirectory;
        return true;
    }
}
