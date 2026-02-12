namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Interface for tasks that can execute in a thread-safe manner within MSBuild's
    /// multithreaded execution model.
    /// </summary>
    public interface IMultiThreadableTask
    {
        /// <summary>
        /// Gets or sets the task execution environment, which provides access to
        /// project directory and environment variables in a thread-safe manner.
        /// </summary>
        TaskEnvironment TaskEnvironment { get; set; }
    }
}
