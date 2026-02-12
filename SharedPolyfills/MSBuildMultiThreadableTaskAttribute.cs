using System;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Attribute that marks a task class as thread-safe for multithreaded execution.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class MSBuildMultiThreadableTaskAttribute : Attribute
    {
    }
}
