using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

/// <summary>
/// Uses a <see cref="Lazy{T}"/> to capture an environment variable at static initialization
/// time. Because the Lazy is static, the value is frozen to whatever the process environment
/// contained when the first task instance triggered initialization — all subsequent task
/// instances see a stale or wrong value.
/// </summary>
public class LazyEnvVarCapture : Task
{
    // BUG: static Lazy captures the env var once for the entire process lifetime.
    private static readonly Lazy<string> CachedValue =
        new(() => Environment.GetEnvironmentVariable("MY_SETTING") ?? string.Empty);

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
