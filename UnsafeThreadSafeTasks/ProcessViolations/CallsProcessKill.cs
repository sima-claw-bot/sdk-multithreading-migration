using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class CallsProcessKill : Task
{
    public override bool Execute()
    {
        Process.GetCurrentProcess().Kill();
        return true; // never reached
    }
}
