using System;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class EnvVarToctou : Task
{
    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Output]
    public string InitialValue { get; set; } = string.Empty;

    [Output]
    public string FinalValue { get; set; } = string.Empty;

    public override bool Execute()
    {
        // BUG: first read — captures the current value.
        InitialValue = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;

        // Simulate work; widens the race window so another thread can modify the variable.
        Thread.Sleep(50);

        // BUG: second read — may see a different value set by a concurrent task.
        FinalValue = Environment.GetEnvironmentVariable(VariableName) ?? string.Empty;

        if (InitialValue != FinalValue)
        {
            Log.LogWarning(
                "Environment variable '{0}' changed between reads: '{1}' -> '{2}'",
                VariableName, InitialValue, FinalValue);
        }

        return true;
    }
}
