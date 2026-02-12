using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.EnvironmentViolations;

/// <summary>
/// Sets an environment variable via Environment.SetEnvironmentVariable and then reads it back.
/// This is unsafe because environment variables are process-global shared state and modifying
/// them can affect other tasks running concurrently.
/// </summary>
public class UsesEnvironmentSetVariable : Task
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Value { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Environment.SetEnvironmentVariable(Name, Value);
        Result = Environment.GetEnvironmentVariable(Name) ?? string.Empty;
        return true;
    }
}
