using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Reads an environment variable via Environment.GetEnvironmentVariable. This is unsafe
/// because environment variables are process-global shared state that can be modified
/// concurrently by other tasks.
/// </summary>
public class TaskGamma03 : Task
{
    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;
        return true;
    }
}
