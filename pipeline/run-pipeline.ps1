<#
.SYNOPSIS
    Pipeline automation for SDK multithreading migration.
.DESCRIPTION
    Phase 1: Iterates over each masked task in MaskedTasks/, reads the task source,
    and generates a migration prompt file in pipeline/prompts/ describing:
      - The task code
      - The goal (make it thread-safe)
      - The available IMultiThreadableTask interface and TaskEnvironment API
      - The AbsolutePath struct
      - What forbidden APIs to replace
    Does NOT reveal the original violation category name.
#>

param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Phase1Only
)

$ErrorActionPreference = 'Stop'

$MaskedTasksDir = Join-Path $RepoRoot "MaskedTasks"
$PromptsDir = Join-Path $PSScriptRoot "prompts"
$SkillsDir = Join-Path $RepoRoot "skills"
$PolyfillsDir = Join-Path $RepoRoot "SharedPolyfills"

# ─── Phase 1: Generate migration prompts ───────────────────────────────────────

function Invoke-Phase1 {
    Write-Host "`n=== Phase 1: Generate Migration Prompts ===" -ForegroundColor Cyan

    # Ensure prompts output directory exists
    if (-not (Test-Path $PromptsDir)) {
        New-Item -ItemType Directory -Path $PromptsDir -Force | Out-Null
    }

    # Read the polyfill source files for inclusion in prompts
    $iMultiThreadableTaskSource = Get-Content (Join-Path $PolyfillsDir "IMultiThreadableTask.cs") -Raw
    $taskEnvironmentSource = Get-Content (Join-Path $PolyfillsDir "TaskEnvironment.cs") -Raw
    $absolutePathSource = Get-Content (Join-Path $PolyfillsDir "AbsolutePath.cs") -Raw
    $attributeSource = Get-Content (Join-Path $PolyfillsDir "MSBuildMultiThreadableTaskAttribute.cs") -Raw

    # Build the forbidden API reference from skills docs
    $forbiddenApiReference = Get-ForbiddenApiReference

    # Discover all masked task .cs files (skip .csproj and non-code files)
    $taskFiles = Get-ChildItem -Path $MaskedTasksDir -Filter "*.cs" -Recurse

    if ($taskFiles.Count -eq 0) {
        Write-Warning "No masked task files found in $MaskedTasksDir"
        return
    }

    $generated = 0
    foreach ($taskFile in $taskFiles) {
        $taskSource = Get-Content $taskFile.FullName -Raw

        # Extract the class name from the source
        $classMatch = [regex]::Match($taskSource, 'public\s+class\s+(\w+)')
        if (-not $classMatch.Success) {
            Write-Warning "  Skipping $($taskFile.Name): could not extract class name"
            continue
        }
        $className = $classMatch.Groups[1].Value

        # Sanitize the source: strip violation category from namespace to avoid
        # revealing the original violation name (e.g. MaskedTasks.PathViolations → MaskedTasks)
        $sanitizedSource = $taskSource -replace 'namespace MaskedTasks\.\w+;', 'namespace MaskedTasks;'
        $sanitizedSource = $sanitizedSource -replace 'namespace MaskedTasks\.\w+\b(?!;)', 'namespace MaskedTasks'

        # Generate the prompt content — intentionally omits violation category
        $promptContent = Build-MigrationPrompt `
            -ClassName $className `
            -TaskSource $sanitizedSource `
            -IMultiThreadableTaskSource $iMultiThreadableTaskSource `
            -TaskEnvironmentSource $taskEnvironmentSource `
            -AbsolutePathSource $absolutePathSource `
            -AttributeSource $attributeSource `
            -ForbiddenApiReference $forbiddenApiReference

        $promptFile = Join-Path $PromptsDir "$className.prompt.md"
        Set-Content -Path $promptFile -Value $promptContent -NoNewline

        Write-Host "  Generated: $className.prompt.md"
        $generated++
    }

    Write-Host "`nPhase 1 complete: generated $generated prompt(s) in pipeline/prompts/" -ForegroundColor Green
}

function Get-ForbiddenApiReference {
    @"
## Forbidden API Replacement Rules

### Must Replace with TaskEnvironment
| Forbidden API | Thread-Safe Replacement |
|---|---|
| ``Path.GetFullPath(path)`` | ``TaskEnvironment.GetAbsolutePath(path)`` |
| ``Path.GetFullPath(path)`` (for canonicalization) | ``TaskEnvironment.GetAbsolutePath(path).GetCanonicalForm()`` |
| ``Environment.GetEnvironmentVariable(name)`` | ``TaskEnvironment.GetEnvironmentVariable(name)`` |
| ``Environment.SetEnvironmentVariable(name, value)`` | ``TaskEnvironment.SetEnvironmentVariable(name, value)`` |
| ``Environment.CurrentDirectory`` | ``TaskEnvironment.ProjectDirectory`` |
| ``new ProcessStartInfo(...)`` | ``TaskEnvironment.GetProcessStartInfo()`` |

### Must Use Absolute Paths (resolve via TaskEnvironment.GetAbsolutePath first)
- ``File.Exists(path)`` — path must be absolute
- ``File.ReadAllText(path)`` — path must be absolute
- ``File.WriteAllText(path)`` — path must be absolute
- ``File.Create(path)`` — path must be absolute
- ``File.Open(path)`` — path must be absolute
- ``File.Copy(source, dest)`` — paths must be absolute
- ``File.Move(source, dest)`` — paths must be absolute
- ``File.Delete(path)`` — path must be absolute
- ``new FileStream(path, ...)`` — path must be absolute
- ``new StreamReader(path)`` — path must be absolute
- ``new StreamWriter(path)`` — path must be absolute
- ``Directory.Exists(path)`` — path must be absolute
- ``Directory.CreateDirectory(path)`` — path must be absolute
- ``Directory.Delete(path)`` — path must be absolute
- ``XDocument.Load(path)`` — path must be absolute
- ``XDocument.Save(path)`` — path must be absolute
- ``FileVersionInfo.GetVersionInfo(path)`` — path must be absolute

### Never Use (must remove or replace with logging)
- ``Environment.Exit()`` / ``Environment.FailFast()`` — forbidden
- ``Process.GetCurrentProcess().Kill()`` — forbidden
- ``Console.WriteLine()`` / ``Console.ReadLine()`` / ``Console.Write()`` — replace with ``Log.LogMessage()``
- ``Console.SetOut()`` / ``Console.SetError()`` / ``Console.SetIn()`` — forbidden
- ``Console.Out`` / ``Console.Error`` / ``Console.In`` — forbidden
"@
}

function Build-MigrationPrompt {
    param(
        [string]$ClassName,
        [string]$TaskSource,
        [string]$IMultiThreadableTaskSource,
        [string]$TaskEnvironmentSource,
        [string]$AbsolutePathSource,
        [string]$AttributeSource,
        [string]$ForbiddenApiReference
    )

    @"
# Migration Prompt: Make ``$ClassName`` Thread-Safe

## Goal

Migrate the MSBuild task ``$ClassName`` to be thread-safe for MSBuild's multithreaded execution model.

You must:
1. Analyze the task source code for any use of forbidden (non-thread-safe) APIs.
2. If forbidden APIs are found:
   - Add the ``[MSBuildMultiThreadableTask]`` attribute to the class.
   - Implement the ``IMultiThreadableTask`` interface (adds ``TaskEnvironment TaskEnvironment { get; set; }``).
   - Replace all forbidden API calls with their thread-safe equivalents via ``TaskEnvironment``.
   - Do **NOT** null-check ``TaskEnvironment``. MSBuild always provides a ``TaskEnvironment`` instance to
     ``IMultiThreadableTask`` implementations, even in single-threaded mode (where it acts as a no-op passthrough).
3. If no forbidden APIs are found:
   - Add only the ``[MSBuildMultiThreadableTask]`` attribute (no interface needed).
4. Implement the ``Execute()`` method body to perform the task's described behavior in a thread-safe way.

## Task Source Code

``````csharp
$TaskSource
``````

## Available Thread-Safety API

### IMultiThreadableTask Interface

``````csharp
$IMultiThreadableTaskSource
``````

### TaskEnvironment Class

``````csharp
$TaskEnvironmentSource
``````

### AbsolutePath Struct

``````csharp
$AbsolutePathSource
``````

### MSBuildMultiThreadableTaskAttribute

``````csharp
$AttributeSource
``````

$ForbiddenApiReference

## Migration Patterns

### Pattern A: Attribute-Only (no forbidden APIs)
For tasks that do pure in-memory transformations with no file I/O, no env vars, no ``Path.GetFullPath()``:
``````csharp
[MSBuildMultiThreadableTask]
public class MyTask : Task
{
    public override bool Execute() { /* no global state access */ }
}
``````

### Pattern B: Interface-Based (uses forbidden APIs)
For tasks using ``Path.GetFullPath``, ``File.*``, ``Environment.*``, ``ProcessStartInfo``:
``````csharp
[MSBuildMultiThreadableTask]
public class MyTask : Task, IMultiThreadableTask
{
    public TaskEnvironment TaskEnvironment { get; set; }

    public override bool Execute()
    {
        // Replace: Path.GetFullPath(somePath)
        // With:    TaskEnvironment.GetAbsolutePath(somePath)

        // Replace: Environment.GetEnvironmentVariable("VAR")
        // With:    TaskEnvironment.GetEnvironmentVariable("VAR")

        // Replace: File.Exists(relativePath)
        // With:    File.Exists(TaskEnvironment.GetAbsolutePath(relativePath))

        // Store absolutized path in a local variable for reuse:
        AbsolutePath absPath = TaskEnvironment.GetAbsolutePath(inputPath);
        // use absPath (implicitly converts to string) in all subsequent file operations
    }
}
``````

### Pattern C: Console Replacement
For tasks using ``Console.*`` APIs, replace with MSBuild logging:
``````csharp
// Replace: Console.WriteLine(message)
// With:    Log.LogMessage(MessageImportance.Normal, message)

// Replace: Console.Error.WriteLine(message)
// With:    Log.LogWarning(message) or Log.LogError(message)
``````

## Important Rules

- Trace ALL path strings through helper methods to catch indirect file API usage.
- ``GetAbsolutePath()`` throws on null/empty — handle null/empty inputs in batch operations.
- For ``Path.GetFullPath()`` used for canonicalization, use ``TaskEnvironment.GetAbsolutePath(path).GetCanonicalForm()``.
- Do **NOT** null-check ``TaskEnvironment`` — use it directly.
- Instance fields are per-task-instance (safe by design). No locking needed for instance state.
- The task must return ``bool`` from ``Execute()``: ``true`` on success, ``false`` on failure.
"@
}

# ─── Main entry point ──────────────────────────────────────────────────────────

Write-Host "Pipeline: SDK Multithreading Migration" -ForegroundColor White
Write-Host "Repository root: $RepoRoot"

Invoke-Phase1
