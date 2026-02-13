using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Fixed version: reads the environment variable from the per-task
/// <see cref="TaskEnvironment"/> snapshot instead of the process-global
/// <see cref="System.Environment"/>, eliminating the TOCTOU race where
/// another thread could modify the variable between the two reads.
/// </summary>
[MSBuildMultiThreadableTask]
public class EnvVarToctou : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string VariableName { get; set; } = string.Empty;

    [Output]
    public string InitialValue { get; set; } = string.Empty;

    [Output]
    public string FinalValue { get; set; } = string.Empty;

    public override bool Execute()
    {
        // Fixed: read from the per-task snapshot, not the process-global environment.
        InitialValue = TaskEnvironment.GetEnvironmentVariable(VariableName) ?? string.Empty;

        Thread.Sleep(50);

        // Second read from the same snapshot — guaranteed to match the first.
        FinalValue = TaskEnvironment.GetEnvironmentVariable(VariableName) ?? string.Empty;

        if (InitialValue != FinalValue)
        {
            Log.LogWarning(
                "Environment variable '{0}' changed between reads: '{1}' -> '{2}'",
                VariableName, InitialValue, FinalValue);
        }

        return true;
    }
}
