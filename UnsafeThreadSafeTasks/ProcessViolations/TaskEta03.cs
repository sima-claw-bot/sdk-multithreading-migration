using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.ProcessViolations;

/// <summary>
/// Calls Process.GetCurrentProcess().Kill() which terminates the process.
/// This is a forbidden API in multithreaded MSBuild.
/// </summary>
public class TaskEta03 : Task
{
    public override bool Execute()
    {
        Process.GetCurrentProcess().Kill();
        return true; // never reached
    }
}
