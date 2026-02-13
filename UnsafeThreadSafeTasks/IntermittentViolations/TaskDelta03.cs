#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.IntermittentViolations;

/// <summary>
/// Maintains a static <see cref="FileSystemWatcher"/> whose Changed event handler writes to
/// an instance field. When multiple task instances run concurrently, the single static
/// watcher delivers notifications to whichever instance last registered its handler,
/// causing events to be routed to the wrong task.
/// </summary>
public class TaskDelta03 : Task
{
    // BUG: static watcher shared across all task instances.
    private static FileSystemWatcher? _watcher;
    private static readonly object Lock = new();

    [Required]
    public string WatchPath { get; set; } = string.Empty;

    [Output]
    public string LastChangedFile { get; set; } = string.Empty;

    public override bool Execute()
    {
        lock (Lock)
        {
            if (_watcher != null)
            {
                _watcher.Dispose();
            }

            // BUG: each task instance replaces the global watcher; the event handler
            // captures *this* instance, but a concurrent task may overwrite it.
            _watcher = new FileSystemWatcher(WatchPath)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _watcher.Changed += (_, e) =>
            {
                // This writes to whichever task instance last set up the watcher,
                // not necessarily the task that owns this directory.
                LastChangedFile = e.FullPath;
            };
        }

        return true;
    }
}
