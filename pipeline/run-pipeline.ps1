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

    Phase 3: Agent invocation and retry framework. For each masked task:
      - Invokes the Copilot CLI agent with the migration prompt from Phase 1
      - Runs the generated tests from Phase 2 (dotnet test --filter)
      - Parses test results and returns success/failure with details
      - Retries up to 5 times per task, appending failure details to the prompt
      - Logs each iteration to pipeline/logs/iteration-N/

    Phase 5: Error analysis and retry. After Phase 3 completes:
      - Parses TRX results to identify which tasks still fail
      - Generates an error analysis summary in pipeline/logs/
      - Re-runs Phases 3-4 for failing tasks only (max 20 total outer iterations)
      - Tracks progress across iterations
      - On success (all tasks pass), proceeds to Phase 6

    Phase 6: Finalization and reporting.
      - Generates a final report comparing agent-fixed versions against known-good fixed versions
      - Outputs pipeline/reports/final-report.md with per-task metrics
      - Metrics include: iterations needed, test pass rates, violation type, exact match status

    Resume capability:
      - Use -StartPhase to resume the pipeline from a specific phase (1, 3, or 6)
      - Use -Iteration to resume within Phase 3 at a specific iteration
#>

param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Phase1Only,
    [switch]$Phase3Only,
    [int]$MaxRetries = 5,
    [int]$MaxOuterIterations = 20,
    [int]$StartPhase = 0,
    [int]$Iteration = 1
)

$ErrorActionPreference = 'Stop'

$MaskedTasksDir = Join-Path $RepoRoot "MaskedTasks"
$FixedTasksDir = Join-Path $RepoRoot "FixedThreadSafeTasks"
$PromptsDir = Join-Path $PSScriptRoot "prompts"
$SkillsDir = Join-Path $RepoRoot "skills"
$PolyfillsDir = Join-Path $RepoRoot "SharedPolyfills"
$LogsDir = Join-Path $PSScriptRoot "logs"
$ReportsDir = Join-Path $PSScriptRoot "reports"
$ConfigFile = Join-Path $PSScriptRoot "config.json"
$TestMappingFile = Join-Path $RepoRoot "pipeline-test-mapping.json"
$SolutionFile = Join-Path $RepoRoot "SdkMultithreadingMigration.slnx"

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

# ─── Phase 3: Agent invocation and retry framework ─────────────────────────────

function Get-PipelineConfig {
    $configContent = Get-Content $ConfigFile -Raw
    return ($configContent | ConvertFrom-Json)
}

function Get-TestMapping {
    $mappingContent = Get-Content $TestMappingFile -Raw
    return ($mappingContent | ConvertFrom-Json)
}

function Get-TaskTestFilter {
    param(
        [object]$TaskMapping
    )

    # Build a dotnet test --filter expression from the fixedTestMethods
    $methods = $TaskMapping.fixedTestMethods
    if (-not $methods -or $methods.Count -eq 0) {
        return $null
    }

    $filterParts = $methods | ForEach-Object { "FullyQualifiedName~$_" }
    return ($filterParts -join " | ")
}

function Invoke-AgentForTask {
    param(
        [string]$PromptContent,
        [string]$ClassName,
        [string]$IterationLogDir,
        [object]$AgentConfig
    )

    $agentCommand = $AgentConfig.command
    $agentFlags = $AgentConfig.flags -join " "
    $agentModel = $AgentConfig.model

    # Write the prompt to a temp file for the agent
    $promptFile = Join-Path $IterationLogDir "prompt.md"
    Set-Content -Path $promptFile -Value $PromptContent -NoNewline

    $agentLogFile = Join-Path $IterationLogDir "agent-output.log"

    # Build the copilot CLI invocation command
    $fullCommand = "$agentCommand $agentFlags --model `"$agentModel`" `"$PromptContent`""

    Write-Host "    Invoking agent for $ClassName..." -ForegroundColor DarkCyan

    try {
        # Invoke copilot CLI: pipe the prompt as the message argument
        $agentOutput = & $agentCommand $agentFlags.Split(' ') --model $agentModel $PromptContent 2>&1
        $agentExitCode = $LASTEXITCODE

        # Save agent output to log
        $agentOutput | Out-File -FilePath $agentLogFile -Encoding utf8

        return @{
            Success  = ($agentExitCode -eq 0)
            ExitCode = $agentExitCode
            Output   = ($agentOutput | Out-String)
            LogFile  = $agentLogFile
        }
    }
    catch {
        $errorMessage = $_.Exception.Message
        $errorMessage | Out-File -FilePath $agentLogFile -Encoding utf8

        return @{
            Success  = $false
            ExitCode = 1
            Output   = $errorMessage
            LogFile  = $agentLogFile
        }
    }
}

function Invoke-TestsForTask {
    param(
        [string]$TestFilter,
        [string]$IterationLogDir
    )

    $testLogFile = Join-Path $IterationLogDir "test-output.log"
    $trxFile = Join-Path $IterationLogDir "results.trx"

    Write-Host "    Running tests with filter: $TestFilter" -ForegroundColor DarkCyan

    try {
        $testOutput = & dotnet test $SolutionFile `
            --filter $TestFilter `
            --logger "trx;LogFileName=$trxFile" `
            --no-build `
            --verbosity normal 2>&1

        $testExitCode = $LASTEXITCODE

        # Save test output to log
        $testOutput | Out-File -FilePath $testLogFile -Encoding utf8

        # Parse test results
        $result = Get-TestResults -TestOutput ($testOutput | Out-String) -TrxFile $trxFile -ExitCode $testExitCode

        return $result
    }
    catch {
        $errorMessage = $_.Exception.Message
        $errorMessage | Out-File -FilePath $testLogFile -Encoding utf8

        return @{
            Success     = $false
            ExitCode    = 1
            TotalTests  = 0
            PassedTests = 0
            FailedTests = 0
            Details     = "Test execution error: $errorMessage"
            LogFile     = $testLogFile
        }
    }
}

function Get-TestResults {
    param(
        [string]$TestOutput,
        [string]$TrxFile,
        [int]$ExitCode
    )

    $totalTests = 0
    $passedTests = 0
    $failedTests = 0
    $failureDetails = @()

    # Parse console output for test summary
    # Matches patterns like "Total tests: 10", "Passed: 8", "Failed: 2"
    if ($TestOutput -match 'Total tests:\s*(\d+)') {
        $totalTests = [int]$Matches[1]
    }
    if ($TestOutput -match '(?m)^\s*Passed\s*:\s*(\d+)') {
        $passedTests = [int]$Matches[1]
    }
    if ($TestOutput -match '(?m)^\s*Failed\s*:\s*(\d+)') {
        $failedTests = [int]$Matches[1]
    }

    # Extract individual failure messages from test output
    $failureLines = ($TestOutput -split "`n") | Where-Object { $_ -match '^\s*Failed\s+\w+' }
    foreach ($line in $failureLines) {
        $failureDetails += $line.Trim()
    }

    # Also try to extract error messages following "Failed" lines
    $outputLines = $TestOutput -split "`n"
    for ($i = 0; $i -lt $outputLines.Count; $i++) {
        if ($outputLines[$i] -match '^\s*Failed\s+(\S+)') {
            $testName = $Matches[1]
            # Collect subsequent indented error lines
            $errorLines = @()
            for ($j = $i + 1; $j -lt $outputLines.Count; $j++) {
                if ($outputLines[$j] -match '^\s{4,}\S') {
                    $errorLines += $outputLines[$j].Trim()
                } else {
                    break
                }
            }
            if ($errorLines.Count -gt 0) {
                $failureDetails += "  $testName : $($errorLines -join ' ')"
            }
        }
    }

    # If we have a TRX file, try parsing it for more detail
    if (Test-Path $TrxFile) {
        try {
            [xml]$trx = Get-Content $TrxFile -Raw
            $ns = @{ t = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010" }

            $counters = $trx.TestRun.ResultSummary.Counters
            if ($counters) {
                $totalTests = [int]$counters.total
                $passedTests = [int]$counters.passed
                $failedTests = [int]$counters.failed
            }

            # Extract failure messages from TRX
            $failedResults = Select-Xml -Xml $trx -XPath "//t:UnitTestResult[@outcome='Failed']" -Namespace $ns
            foreach ($result in $failedResults) {
                $testNameFromTrx = $result.Node.testName
                $errorMsg = $result.Node.Output.ErrorInfo.Message
                if ($errorMsg) {
                    $failureDetails += "$testNameFromTrx : $errorMsg"
                }
            }
        }
        catch {
            # TRX parsing is best-effort; fall back to console output
        }
    }

    $success = ($ExitCode -eq 0) -and ($failedTests -eq 0)
    $detailsText = if ($failureDetails.Count -gt 0) {
        $failureDetails -join "`n"
    } else {
        if ($success) { "All tests passed." } else { "Tests failed. Exit code: $ExitCode" }
    }

    return @{
        Success     = $success
        ExitCode    = $ExitCode
        TotalTests  = $totalTests
        PassedTests = $passedTests
        FailedTests = $failedTests
        Details     = $detailsText
        LogFile     = ""
    }
}

function Build-RetryPrompt {
    param(
        [string]$OriginalPrompt,
        [string]$FailureDetails,
        [int]$Iteration
    )

    $retryAddendum = @"

## Retry Attempt $Iteration — Previous Failure Details

The previous migration attempt failed tests. Please fix the issues described below
and try again. Do NOT repeat the same mistakes.

### Test Failure Details

$FailureDetails
"@

    return $OriginalPrompt + "`n" + $retryAddendum
}

function Write-Phase3Progress {
    param(
        [string]$ProgressFile,
        [array]$TaskResults,
        [string]$Status
    )

    $progress = @{
        status    = $Status
        timestamp = (Get-Date -Format "o")
        summary   = @{
            total  = $TaskResults.Count
            passed = ($TaskResults | Where-Object { $_.status -eq "passed" }).Count
            failed = ($TaskResults | Where-Object { $_.status -eq "failed" }).Count
            pending = ($TaskResults | Where-Object { $_.status -eq "pending" }).Count
            running = ($TaskResults | Where-Object { $_.status -eq "running" }).Count
            skipped = ($TaskResults | Where-Object { $_.status -eq "skipped" }).Count
        }
        tasks     = $TaskResults
    }

    $progress | ConvertTo-Json -Depth 10 | Set-Content -Path $ProgressFile -Encoding utf8
}

function Invoke-Phase3 {
    param(
        [string[]]$TaskFilter = @()
    )

    Write-Host "`n=== Phase 3: Agent Invocation & Retry Framework ===" -ForegroundColor Cyan

    # Load configuration
    $config = Get-PipelineConfig
    $agentConfig = $config.agent
    $mapping = Get-TestMapping

    if (-not $mapping.tasks -or $mapping.tasks.Count -eq 0) {
        Write-Warning "No tasks found in pipeline-test-mapping.json"
        return @()
    }

    # If TaskFilter is provided, only process those tasks
    $tasksToProcess = $mapping.tasks
    if ($TaskFilter.Count -gt 0) {
        $tasksToProcess = @($mapping.tasks | Where-Object { $TaskFilter -contains $_.classes[0] })
        Write-Host "  Filtering to $($tasksToProcess.Count) task(s): $($TaskFilter -join ', ')" -ForegroundColor DarkCyan
    }

    # Ensure logs directory exists
    if (-not (Test-Path $LogsDir)) {
        New-Item -ItemType Directory -Path $LogsDir -Force | Out-Null
    }

    $progressFile = Join-Path $LogsDir "phase3-progress.json"

    # Initialize per-task progress tracking
    $taskProgress = @()
    foreach ($t in $mapping.tasks) {
        $taskProgress += @{
            className  = $t.classes[0]
            status     = "pending"
            iterations = 0
            details    = ""
        }
    }

    # Write initial progress
    Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"

    $results = @()
    $taskIndex = 0

    foreach ($task in $mapping.tasks) {
        $className = $task.classes[0]
        $maskedFile = Join-Path $RepoRoot $task.maskedFile

        Write-Host "`n  Processing task: $className" -ForegroundColor Yellow

        # Mark task as running in progress
        $taskProgress[$taskIndex].status = "running"
        Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"

        # Check for prompt from Phase 1
        $promptFile = Join-Path $PromptsDir "$className.prompt.md"
        if (-not (Test-Path $promptFile)) {
            Write-Warning "  No prompt found for $className (expected: $promptFile). Skipping."
            $results += @{
                ClassName = $className
                Success   = $false
                Iteration = 0
                Details   = "No prompt file found"
            }
            $taskProgress[$taskIndex].status = "skipped"
            $taskProgress[$taskIndex].details = "No prompt file found"
            Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"
            $taskIndex++
            continue
        }

        $originalPrompt = Get-Content $promptFile -Raw

        # Get test filter for this task
        $testFilter = Get-TaskTestFilter -TaskMapping $task
        if (-not $testFilter) {
            Write-Warning "  No test methods found for $className. Skipping."
            $results += @{
                ClassName = $className
                Success   = $false
                Iteration = 0
                Details   = "No test methods defined"
            }
            $taskProgress[$taskIndex].status = "skipped"
            $taskProgress[$taskIndex].details = "No test methods defined"
            Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"
            $taskIndex++
            continue
        }

        $success = $false
        $currentPrompt = $originalPrompt
        $lastDetails = ""
        $finalIteration = 0

        for ($iteration = 1; $iteration -le $MaxRetries; $iteration++) {
            Write-Host "  Iteration $iteration/$MaxRetries for $className" -ForegroundColor DarkYellow
            $finalIteration = $iteration

            # Update progress with current iteration
            $taskProgress[$taskIndex].iterations = $iteration
            Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"

            # Create iteration log directory
            $taskLogDir = Join-Path $LogsDir $className
            $iterationLogDir = Join-Path $taskLogDir "iteration-$iteration"
            if (-not (Test-Path $iterationLogDir)) {
                New-Item -ItemType Directory -Path $iterationLogDir -Force | Out-Null
            }

            # Step 1: Invoke the agent with the current prompt
            $agentResult = Invoke-AgentForTask `
                -PromptContent $currentPrompt `
                -ClassName $className `
                -IterationLogDir $iterationLogDir `
                -AgentConfig $agentConfig

            # Save iteration metadata
            $iterationMeta = @{
                className  = $className
                iteration  = $iteration
                timestamp  = (Get-Date -Format "o")
                agentSuccess = $agentResult.Success
                agentExitCode = $agentResult.ExitCode
            }
            $iterationMeta | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $iterationLogDir "metadata.json")

            if (-not $agentResult.Success) {
                Write-Host "    Agent invocation failed (exit code: $($agentResult.ExitCode))" -ForegroundColor Red
                $lastDetails = "Agent invocation failed: $($agentResult.Output)"

                # Build retry prompt with failure details
                $currentPrompt = Build-RetryPrompt `
                    -OriginalPrompt $originalPrompt `
                    -FailureDetails $lastDetails `
                    -Iteration $iteration

                continue
            }

            Write-Host "    Agent completed successfully, running tests..." -ForegroundColor DarkCyan

            # Step 2: Run tests to validate the migration
            $testResult = Invoke-TestsForTask `
                -TestFilter $testFilter `
                -IterationLogDir $iterationLogDir

            # Update iteration metadata with test results
            $iterationMeta.testSuccess = $testResult.Success
            $iterationMeta.totalTests = $testResult.TotalTests
            $iterationMeta.passedTests = $testResult.PassedTests
            $iterationMeta.failedTests = $testResult.FailedTests
            $iterationMeta | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $iterationLogDir "metadata.json")

            if ($testResult.Success) {
                Write-Host "    All tests passed for $className on iteration $iteration!" -ForegroundColor Green
                $success = $true
                $lastDetails = $testResult.Details
                break
            }

            Write-Host "    Tests failed ($($testResult.FailedTests)/$($testResult.TotalTests) failed)" -ForegroundColor Red
            $lastDetails = $testResult.Details

            # Build retry prompt with failure details for next iteration
            if ($iteration -lt $MaxRetries) {
                $currentPrompt = Build-RetryPrompt `
                    -OriginalPrompt $originalPrompt `
                    -FailureDetails $lastDetails `
                    -Iteration $iteration
            }
        }

        $results += @{
            ClassName = $className
            Success   = $success
            Iteration = $finalIteration
            Details   = $lastDetails
        }

        # Update per-task progress
        $taskProgress[$taskIndex].status = if ($success) { "passed" } else { "failed" }
        $taskProgress[$taskIndex].iterations = $finalIteration
        $taskProgress[$taskIndex].details = $lastDetails
        Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "running"

        if ($success) {
            Write-Host "  [PASS] $className (iteration $finalIteration)" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] $className after $MaxRetries retries" -ForegroundColor Red
        }

        $taskIndex++
    }

    # Write final progress
    Write-Phase3Progress -ProgressFile $progressFile -TaskResults $taskProgress -Status "completed"

    # Print summary
    $passedCount = ($results | Where-Object { $_.Success }).Count
    $failedCount = ($results | Where-Object { -not $_.Success }).Count
    Write-Host "`nPhase 3 complete: $passedCount passed, $failedCount failed out of $($results.Count) tasks" -ForegroundColor Green

    return $results
}

# ─── Phase 6: Finalization and reporting ────────────────────────────────────────

function Format-MarkdownTable {
    param(
        [string[]]$Headers,
        [string[][]]$Rows
    )

    $lines = @()
    $lines += "| " + ($Headers -join " | ") + " |"
    $lines += "|" + (($Headers | ForEach-Object { "---" }) -join "|") + "|"
    foreach ($row in $Rows) {
        $lines += "| " + ($row -join " | ") + " |"
    }
    return $lines
}

function Compare-AgentVsFixed {
    param(
        [string]$AgentFile,
        [string]$FixedFile
    )

    if ([string]::IsNullOrWhiteSpace($AgentFile) -or [string]::IsNullOrWhiteSpace($FixedFile)) {
        return @{
            Match     = $false
            AgentExists = $false
            FixedExists = $false
            AgentHasAttribute = $false
            AgentHasInterface = $false
            FixedHasInterface = $false
            Details   = "File path(s) not configured"
        }
    }

    $agentExists = (Test-Path $AgentFile) -and -not (Test-Path $AgentFile -PathType Container)
    $fixedExists = (Test-Path $FixedFile) -and -not (Test-Path $FixedFile -PathType Container)

    if (-not $agentExists -or -not $fixedExists) {
        return @{
            Match     = $false
            AgentExists = $agentExists
            FixedExists = $fixedExists
            AgentHasAttribute = $false
            AgentHasInterface = $false
            FixedHasInterface = $false
            Details   = "File(s) missing"
        }
    }

    $agentContent = (Get-Content $AgentFile -Raw).Trim()
    $fixedContent = (Get-Content $FixedFile -Raw).Trim()

    # Check for key structural markers in agent output
    $agentHasAttribute = $agentContent -match '\[MSBuildMultiThreadableTask\]'
    $agentHasInterface = $agentContent -match 'IMultiThreadableTask'
    $fixedHasInterface = $fixedContent -match 'IMultiThreadableTask'

    $exactMatch = $agentContent -eq $fixedContent

    return @{
        Match           = $exactMatch
        AgentExists     = $true
        FixedExists     = $true
        AgentHasAttribute = $agentHasAttribute
        AgentHasInterface = $agentHasInterface
        FixedHasInterface = $fixedHasInterface
        Details         = if ($exactMatch) { "Exact match" } else { "Structural differences" }
    }
}

function Get-TaskMetrics {
    param(
        [object]$TaskMapping,
        [hashtable]$Phase3Lookup
    )

    $taskMetrics = @()

    foreach ($task in $TaskMapping.tasks) {
        $className = $task.classes[0]
        $category = $task.category
        $fixedFilePath = if ($task.fixedFile) { Join-Path $RepoRoot $task.fixedFile } else { "" }
        $unsafeFilePath = if ($task.unsafeFile) { Join-Path $RepoRoot $task.unsafeFile } else { "" }

        # Get Phase 3 result for this task
        $phase3Result = $Phase3Lookup[$className]
        $status = if ($phase3Result) { $phase3Result.status } else { "not_run" }
        $iterations = if ($phase3Result) { $phase3Result.iterations } else { 0 }

        # Calculate test pass rate from iteration logs
        $testPassRate = "N/A"
        $totalTests = 0
        $passedTests = 0
        $failedTests = 0

        if ($iterations -gt 0) {
            $lastIterDir = Join-Path $LogsDir "$className\iteration-$iterations"
            $metadataFile = Join-Path $lastIterDir "metadata.json"
            if (Test-Path $metadataFile) {
                try {
                    $meta = Get-Content $metadataFile -Raw | ConvertFrom-Json
                    $totalTests = if ($meta.totalTests) { [int]$meta.totalTests } else { 0 }
                    $passedTests = if ($meta.passedTests) { [int]$meta.passedTests } else { 0 }
                    $failedTests = if ($meta.failedTests) { [int]$meta.failedTests } else { 0 }
                    if ($totalTests -gt 0) {
                        $pct = [math]::Round(($passedTests / $totalTests) * 100, 1)
                        $testPassRate = "$pct% ($passedTests/$totalTests)"
                    }
                }
                catch {
                    # Best-effort metadata parsing
                }
            }
        }

        # Compare agent output against known-good fixed version
        $comparison = Compare-AgentVsFixed -AgentFile $unsafeFilePath -FixedFile $fixedFilePath

        # Determine status icon and count separately
        $statusIcon = switch ($status) {
            "passed"  { "✅" }
            "failed"  { "❌" }
            "skipped" { "⏭️" }
            default   { "⬜" }
        }

        $taskMetrics += @{
            ClassName      = $className
            Category       = $category
            Status         = $status
            StatusIcon     = $statusIcon
            Iterations     = $iterations
            TestPassRate   = $testPassRate
            TotalTests     = $totalTests
            PassedTests    = $passedTests
            FailedTests    = $failedTests
            ExactMatch     = $comparison.Match
            HasAttribute   = $comparison.AgentHasAttribute
            HasInterface   = $comparison.AgentHasInterface
            FixedHasInterface = $comparison.FixedHasInterface
        }
    }

    return $taskMetrics
}

function Format-ReportMarkdown {
    param(
        [array]$TaskMetrics,
        [int]$TotalTasks
    )

    # Count statuses explicitly
    $passedCount = ($TaskMetrics | Where-Object { $_.Status -eq "passed" }).Count
    $failedCount = ($TaskMetrics | Where-Object { $_.Status -eq "failed" }).Count
    $skippedCount = ($TaskMetrics | Where-Object { $_.Status -eq "skipped" -or $_.Status -eq "not_run" }).Count
    $notRunCount = $TotalTasks - $passedCount - $failedCount - $skippedCount

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

    $reportLines = @()
    $reportLines += "# Final Pipeline Report"
    $reportLines += ""
    $reportLines += "**Generated:** $timestamp"
    $reportLines += "**Pipeline:** SDK Multithreading Migration"
    $reportLines += ""

    # Summary table
    $reportLines += "## Summary"
    $reportLines += ""
    $summaryRows = @(
        @("Total Tasks", "$TotalTasks"),
        @("Passed", "$passedCount"),
        @("Failed", "$failedCount"),
        @("Skipped", "$skippedCount")
    )
    if ($notRunCount -gt 0) {
        $summaryRows += ,@("Not Run", "$notRunCount")
    }
    $reportLines += Format-MarkdownTable -Headers @("Metric", "Count") -Rows $summaryRows
    $reportLines += ""

    # Group by violation category
    $categories = $TaskMetrics | Group-Object -Property { $_.Category } | Sort-Object Name
    $reportLines += "## Results by Violation Category"
    $reportLines += ""

    foreach ($cat in $categories) {
        $catPassed = ($cat.Group | Where-Object { $_.Status -eq "passed" }).Count
        $catTotal = $cat.Group.Count
        $reportLines += "### $($cat.Name) ($catPassed/$catTotal passed)"
        $reportLines += ""

        $catRows = @()
        foreach ($metric in $cat.Group) {
            $matchLabel = if ($metric.ExactMatch) { "✅ Exact" } else { "❌ Differs" }
            $catRows += ,@("$($metric.ClassName)", "$($metric.StatusIcon) $($metric.Status)", "$($metric.Iterations)", "$($metric.TestPassRate)", "$matchLabel")
        }
        $reportLines += Format-MarkdownTable -Headers @("Task", "Status", "Iterations", "Test Pass Rate", "Matches Fixed") -Rows $catRows
        $reportLines += ""
    }

    # Per-task detailed metrics
    $reportLines += "## Detailed Per-Task Metrics"
    $reportLines += ""
    $detailRows = @()
    $idx = 1
    foreach ($metric in $TaskMetrics) {
        $matchLabel = if ($metric.ExactMatch) { "Yes" } else { "No" }
        $detailRows += ,@("$idx", "$($metric.ClassName)", "$($metric.Category)", "$($metric.StatusIcon) $($metric.Status)", "$($metric.Iterations)", "$($metric.PassedTests)", "$($metric.FailedTests)", "$($metric.TestPassRate)", "$matchLabel")
        $idx++
    }
    $reportLines += Format-MarkdownTable -Headers @("#", "Task", "Category", "Status", "Iterations", "Tests Passed", "Tests Failed", "Pass Rate", "Exact Match") -Rows $detailRows
    $reportLines += ""

    # Structural comparison summary
    $exactMatches = ($TaskMetrics | Where-Object { $_.ExactMatch }).Count
    $structuralMatches = ($TaskMetrics | Where-Object { $_.HasAttribute -and ($_.HasInterface -eq $_.FixedHasInterface) }).Count
    $reportLines += "## Agent vs Known-Good Comparison"
    $reportLines += ""
    $comparisonRows = @(
        @("Exact code matches", "$exactMatches / $TotalTasks"),
        @("Correct structural pattern (attribute + interface)", "$structuralMatches / $TotalTasks")
    )
    $reportLines += Format-MarkdownTable -Headers @("Metric", "Count") -Rows $comparisonRows
    $reportLines += ""

    # Iteration distribution
    $reportLines += "## Iteration Distribution"
    $reportLines += ""
    $iterGroups = $TaskMetrics | Where-Object { $_.Iterations -gt 0 } | Group-Object -Property { $_.Iterations } | Sort-Object { [int]$_.Name }
    if ($iterGroups.Count -gt 0) {
        $iterRows = @()
        foreach ($iterGroup in $iterGroups) {
            $iterRows += ,@("$($iterGroup.Name)", "$($iterGroup.Count)")
        }
        $reportLines += Format-MarkdownTable -Headers @("Iterations Needed", "Task Count") -Rows $iterRows
    } else {
        $reportLines += "*No iteration data available (Phase 3 has not been run).*"
    }
    $reportLines += ""

    return @{
        Content      = ($reportLines -join "`n")
        PassedCount  = $passedCount
        FailedCount  = $failedCount
        SkippedCount = $skippedCount
    }
}

function Write-FinalReport {
    param(
        [string]$ReportContent,
        [string]$OutputPath
    )

    $reportDir = Split-Path $OutputPath -Parent
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    Set-Content -Path $OutputPath -Value $ReportContent -NoNewline -Encoding utf8
}

function Invoke-Phase6 {
    Write-Host "`n=== Phase 6: Finalization & Reporting ===" -ForegroundColor Cyan

    $mapping = Get-TestMapping

    if (-not $mapping.tasks -or $mapping.tasks.Count -eq 0) {
        Write-Warning "No tasks found in pipeline-test-mapping.json"
        return
    }

    # Load Phase 3 progress if available
    $progressFile = Join-Path $LogsDir "phase3-progress.json"
    $phase3Data = $null
    if (Test-Path $progressFile) {
        $phase3Data = Get-Content $progressFile -Raw | ConvertFrom-Json
    }

    # Build a lookup of Phase 3 results by class name
    $phase3Lookup = @{}
    if ($phase3Data -and $phase3Data.tasks) {
        foreach ($task in $phase3Data.tasks) {
            $phase3Lookup[$task.className] = $task
        }
    }

    # Collect per-task metrics
    $taskMetrics = Get-TaskMetrics -TaskMapping $mapping -Phase3Lookup $phase3Lookup

    # Generate report markdown
    $totalTasks = $mapping.tasks.Count
    $report = Format-ReportMarkdown -TaskMetrics $taskMetrics -TotalTasks $totalTasks

    # Write report to disk
    $reportFile = Join-Path $ReportsDir "final-report.md"
    Write-FinalReport -ReportContent $report.Content -OutputPath $reportFile

    Write-Host "`nPhase 6 complete: report generated at pipeline/reports/final-report.md" -ForegroundColor Green
    Write-Host "  Total: $totalTasks | Passed: $($report.PassedCount) | Failed: $($report.FailedCount) | Skipped: $($report.SkippedCount)"
}

# ─── Main entry point ──────────────────────────────────────────────────────────

Write-Host "Pipeline: SDK Multithreading Migration" -ForegroundColor White
Write-Host "Repository root: $RepoRoot"

if ($StartPhase -gt 0) {
    # Resume mode: run from the specified phase onward
    Write-Host "Resuming from Phase $StartPhase (Iteration: $Iteration)" -ForegroundColor Yellow

    if ($StartPhase -le 1) {
        Invoke-Phase1
    }
    if ($StartPhase -le 3) {
        Invoke-Phase3
    }
    if ($StartPhase -le 6) {
        Invoke-Phase6
    }
} elseif ($Phase3Only) {
    Invoke-Phase3
} else {
    Invoke-Phase1
    if (-not $Phase1Only) {
        Invoke-Phase3
        Invoke-Phase6
    }
}
