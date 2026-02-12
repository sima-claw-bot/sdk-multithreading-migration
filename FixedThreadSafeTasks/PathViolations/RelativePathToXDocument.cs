using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace FixedThreadSafeTasks.PathViolations;

/// <summary>
/// Fixed version: resolves relative paths via TaskEnvironment before calling XDocument.Load.
/// </summary>
[MSBuildMultiThreadableTask]
public class RelativePathToXDocument : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; } = new();

    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        string absolutePath = TaskEnvironment.GetAbsolutePath(InputPath);
        var doc = XDocument.Load(absolutePath);
        Result = doc.Root?.Name.LocalName ?? string.Empty;
        return true;
    }
}
