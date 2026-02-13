using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace MaskedTasks.ComplexViolations;

/// <summary>
/// Hides a <see cref="Path.GetFullPath"/> call behind 3+ levels of private method calls.
/// The <see cref="Execute"/> method looks clean, but the violation is buried deep in the
/// call chain: Execute → PrepareOutput → BuildFullPath → NormalizePath, where the final
/// method calls <see cref="Path.GetFullPath"/> against the process-wide CWD.
/// </summary>
public class TaskAlpha04 : Task
{
    [Required]
    public string InputPath { get; set; } = string.Empty;

    [Output]
    public string OutputPath { get; set; } = string.Empty;

    public override bool Execute()
    {
        // TODO: Implement the thread-safe version of this task.
        // See the XML doc comment above for a description of what this task does
        // and what thread-safety violation it contains.
        throw new System.NotImplementedException();
    }

    private string PrepareOutput(string path)
    {
        // Level 2: still looks harmless — just delegates further.
        var trimmed = path.Trim();
        return BuildFullPath(trimmed);
    }

    private string BuildFullPath(string path)
    {
        // Level 3: delegates one more time.
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return NormalizePath(path);
    }

    private string NormalizePath(string path)
    {
        // BUG: Path.GetFullPath resolves relative paths against the process CWD.
        // This is 3 levels deep from Execute(), making it hard to spot.
        return Path.GetFullPath(path);
    }
}
