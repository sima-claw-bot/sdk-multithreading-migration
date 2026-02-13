using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace UnsafeThreadSafeTasks.PathViolations;

/// <summary>
/// MSBuild task that may contain thread-safety issues.
/// </summary>
public class RelativePathToXDocument : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string Result { get; set; } = string.Empty;

    public override bool Execute()
    {
        var doc = XDocument.Load(InputPath);
        Result = doc.Root?.Name.LocalName ?? string.Empty;
        return true;
    }
}
