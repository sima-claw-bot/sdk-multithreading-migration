using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.PathViolations;

/// <summary>
/// Calls XDocument.Load with a relative path. This is unsafe because XDocument.Load resolves
/// relative paths against the process working directory, not the project directory.
/// </summary>
public class TaskZeta04 : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

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
