using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
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
        Result = CachedValue.Value;
        return true;
    }
}
