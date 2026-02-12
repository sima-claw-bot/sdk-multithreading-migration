#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Provides an IMultiThreadableTask with access to a run-time execution environment
    /// including environment variables, file paths, and process management capabilities.
    /// </summary>
    public class TaskEnvironment
    {
        private readonly Dictionary<string, string> _environmentVariables = new();

        /// <summary>
        /// Gets or sets the project directory for the task execution.
        /// </summary>
        public string ProjectDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Converts a relative or absolute path string to an absolute path,
        /// resolving relative paths against <see cref="ProjectDirectory"/>.
        /// </summary>
        public AbsolutePath GetAbsolutePath(string path)
        {
            string fullPath = Path.GetFullPath(Path.Combine(ProjectDirectory, path));
            return new AbsolutePath(fullPath);
        }

        /// <summary>
        /// Gets the value of an environment variable.
        /// </summary>
        public string? GetEnvironmentVariable(string name)
        {
            return _environmentVariables.TryGetValue(name, out string? value) ? value : null;
        }

        /// <summary>
        /// Sets the value of an environment variable.
        /// </summary>
        public void SetEnvironmentVariable(string name, string value)
        {
            if (name is null) throw new System.ArgumentNullException(nameof(name));
            if (value is null) throw new System.ArgumentNullException(nameof(value));

            _environmentVariables[name] = value;
        }

        /// <summary>
        /// Creates a new ProcessStartInfo configured for the current task execution environment.
        /// </summary>
        public ProcessStartInfo GetProcessStartInfo()
        {
            var startInfo = new ProcessStartInfo
            {
                WorkingDirectory = ProjectDirectory
            };

            foreach (KeyValuePair<string, string> kvp in _environmentVariables)
            {
                startInfo.Environment[kvp.Key] = kvp.Value;
            }

            return startInfo;
        }
    }
}
