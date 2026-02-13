#nullable enable
using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.IntermittentViolations;

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
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }
}
