using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Fixed version: reads the environment variable from the per-task
/// <see cref="TaskEnvironment"/> snapshot at execution time instead of
/// using a static <see cref="System.Lazy{T}"/> that freezes the value
/// for the entire process lifetime.
/// </summary>
[MSBuildMultiThreadableTask]
public class LazyEnvVarCapture : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        Result = TaskEnvironment.GetEnvironmentVariable("MY_SETTING") ?? string.Empty;
        return true;
    }
}
