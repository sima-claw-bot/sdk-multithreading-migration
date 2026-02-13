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

    Phase 6: Finalization and reporting. After all phases complete:
      - Compares agent-fixed versions against known-good fixed versions
      - Generates pipeline/reports/final-report.md with per-task metrics
      - Metrics include: iterations needed, test pass rates, violation type
      - Summary statistics across all tasks and categories

    Resume capability:
      - Use -StartPhase N to resume from phase N (1, 3, 5, or 6)
#>

param(
    [string]$RepoRoot = (Split-Path $PSScriptRoot -Parent),
    [switch]$Phase1Only,
    [switch]$Phase3Only,
    [switch]$Phase5Only,
    [switch]$Phase6Only,
    [ValidateSet(1,3,5,6)]
    [int]$StartPhase = 1,
    [int]$MaxRetries = 5,
    [int]$MaxOuterIterations = 20
)

$ErrorActionPreference = 'Stop'

$MaskedTasksDir = Join-Path $RepoRoot "MaskedTasks"
$PromptsDir = Join-Path $PSScriptRoot "prompts"
$SkillsDir = Join-Path $RepoRoot "skills"
$PolyfillsDir = Join-Path $RepoRoot "SharedPolyfills"
$LogsDir = Join-Path $PSScriptRoot "logs"
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

function Compare-TaskFiles {
    param(
        [string]$AgentFile,
        [string]$KnownGoodFile,
        [string[]]$StructuralPatterns = @(
            '\[MSBuildMultiThreadableTask\]',
            'IMultiThreadableTask',
            'TaskEnvironment'
        )
    )

    if (-not $AgentFile -or -not (Test-Path $AgentFile -PathType Leaf)) {
        return @{ Match = $false; Reason = "Agent-fixed file not found" }
    }
    if (-not $KnownGoodFile -or -not (Test-Path $KnownGoodFile -PathType Leaf)) {
        return @{ Match = $false; Reason = "Known-good file not found" }
    }

    $agentContent = (Get-Content $AgentFile -Raw).Trim() -replace '\r\n', "`n"
    $knownGoodContent = (Get-Content $KnownGoodFile -Raw).Trim() -replace '\r\n', "`n"

    if ($agentContent -eq $knownGoodContent) {
        return @{ Match = $true; Reason = "Exact match" }
    }

    # Check structural similarity using configurable patterns
    $structMatch = $true
    foreach ($pattern in $StructuralPatterns) {
        $agentHas = $agentContent -match $pattern
        $knownHas = $knownGoodContent -match $pattern
        if ($agentHas -ne $knownHas) {
            $structMatch = $false
            break
        }
    }

    if ($structMatch) {
        return @{ Match = $false; Reason = "Structural match (same APIs used, different implementation)" }
    }
    return @{ Match = $false; Reason = "Different structure" }
}

function Get-TaskIterationMetrics {
    param(
        [string]$ClassName
    )

    $taskLogDir = Join-Path $LogsDir $ClassName
    $iterations = 0
    $totalTests = 0
    $passedTests = 0
    $failedTests = 0
    $finalSuccess = $false

    if (Test-Path $taskLogDir) {
        $iterDirs = Get-ChildItem -Path $taskLogDir -Directory -Filter "iteration-*" | Sort-Object Name
        $iterations = $iterDirs.Count

        # Read the last iteration's metadata for final results
        if ($iterDirs.Count -gt 0) {
            $lastIterDir = $iterDirs[-1]
            $metaFile = Join-Path $lastIterDir.FullName "metadata.json"
            if (Test-Path $metaFile) {
                try {
                    $meta = Get-Content $metaFile -Raw | ConvertFrom-Json
                    $totalTests = if ($meta.totalTests) { $meta.totalTests } else { 0 }
                    $passedTests = if ($meta.passedTests) { $meta.passedTests } else { 0 }
                    $failedTests = if ($meta.failedTests) { $meta.failedTests } else { 0 }
                    $finalSuccess = if ($meta.testSuccess) { $meta.testSuccess } else { $false }
                }
                catch {
                    # metadata parsing is best-effort
                }
            }
        }
    }

    # Also check phase3 progress file for additional data
    $progressFile = Join-Path $LogsDir "phase3-progress.json"
    if (Test-Path $progressFile) {
        try {
            $progress = Get-Content $progressFile -Raw | ConvertFrom-Json
            $taskEntry = $progress.tasks | Where-Object { $_.className -eq $ClassName }
            if ($taskEntry) {
                if ($taskEntry.iterations -and $taskEntry.iterations -gt $iterations) {
                    $iterations = $taskEntry.iterations
                }
                if ($taskEntry.status -eq "passed") {
                    $finalSuccess = $true
                }
            }
        }
        catch {
            # progress parsing is best-effort
        }
    }

    return @{
        Iterations  = $iterations
        TotalTests  = $totalTests
        PassedTests = $passedTests
        FailedTests = $failedTests
        Success     = $finalSuccess
        PassRate    = if ($totalTests -gt 0) { [math]::Round(($passedTests / $totalTests) * 100, 1) } else { 0 }
    }
}

function Get-GroupMetrics {
    param(
        [array]$Tasks
    )

    $passed = ($Tasks | Where-Object { $_.Success }).Count
    $failed = $Tasks.Count - $passed
    $avgIterations = if ($Tasks.Count -gt 0) {
        [math]::Round(($Tasks | Measure-Object -Property Iterations -Average).Average, 1)
    } else { 0 }
    $avgPassRate = if ($Tasks.Count -gt 0) {
        [math]::Round(($Tasks | Measure-Object -Property PassRate -Average).Average, 1)
    } else { 0 }

    return @{
        Total         = $Tasks.Count
        Passed        = $passed
        Failed        = $failed
        AvgIterations = $avgIterations
        AvgPassRate   = $avgPassRate
    }
}

function Build-SummarySection {
    param(
        [hashtable]$Metrics
    )

    $successRate = [math]::Round(($Metrics.Passed / [math]::Max($Metrics.Total, 1)) * 100, 1)

    $lines = @()
    $lines += "## Summary"
    $lines += ""
    $lines += "| Metric | Value |"
    $lines += "|--------|-------|"
    $lines += "| Total tasks | $($Metrics.Total) |"
    $lines += "| Passed | $($Metrics.Passed) |"
    $lines += "| Failed | $($Metrics.Failed) |"
    $lines += "| Success rate | $successRate% |"
    $lines += "| Avg iterations | $($Metrics.AvgIterations) |"
    $lines += "| Avg test pass rate | $($Metrics.AvgPassRate)% |"
    $lines += ""
    return $lines
}

function Build-CategoryBreakdown {
    param(
        [array]$TaskData
    )

    $categories = $TaskData | Group-Object -Property Category | Sort-Object Name

    $lines = @()
    $lines += "## Results by Violation Category"
    $lines += ""
    $lines += "| Category | Tasks | Passed | Failed | Avg Iterations | Avg Pass Rate |"
    $lines += "|----------|-------|--------|--------|----------------|---------------|"

    foreach ($cat in $categories) {
        $m = Get-GroupMetrics -Tasks $cat.Group
        $lines += "| $($cat.Name) | $($m.Total) | $($m.Passed) | $($m.Failed) | $($m.AvgIterations) | $($m.AvgPassRate)% |"
    }
    $lines += ""
    return $lines
}

function Build-PerTaskTable {
    param(
        [array]$TaskData
    )

    $lines = @()
    $lines += "## Per-Task Results"
    $lines += ""
    $lines += "| Task | Category | Status | Iterations | Tests | Pass Rate | Comparison |"
    $lines += "|------|----------|--------|------------|-------|-----------|------------|"

    foreach ($td in ($TaskData | Sort-Object Category, ClassName)) {
        $status = if ($td.Success) { "✅ Pass" } else { "❌ Fail" }
        $testInfo = "$($td.PassedTests)/$($td.TotalTests)"
        $lines += "| $($td.ClassName) | $($td.Category) | $status | $($td.Iterations) | $testInfo | $($td.PassRate)% | $($td.Comparison) |"
    }
    $lines += ""
    return $lines
}

function Build-FailedTasksDetail {
    param(
        [array]$TaskData
    )

    $failedTaskData = @($TaskData | Where-Object { -not $_.Success })
    if ($failedTaskData.Count -eq 0) {
        return @()
    }

    $lines = @()
    $lines += "## Failed Tasks"
    $lines += ""
    foreach ($ft in $failedTaskData) {
        $lines += "### $($ft.ClassName)"
        $lines += ""
        $lines += "- **Category:** $($ft.Category)"
        $lines += "- **Iterations attempted:** $($ft.Iterations)"
        $lines += "- **Test pass rate:** $($ft.PassRate)% ($($ft.PassedTests)/$($ft.TotalTests))"
        $lines += "- **Comparison:** $($ft.Comparison)"
        $lines += ""
    }
    return $lines
}

function Invoke-Phase6 {
    Write-Host "`n=== Phase 6: Finalization & Reporting ===" -ForegroundColor Cyan

    $mapping = Get-TestMapping
    $ReportsDir = Join-Path $PSScriptRoot "reports"

    # Ensure reports directory exists
    if (-not (Test-Path $ReportsDir)) {
        New-Item -ItemType Directory -Path $ReportsDir -Force | Out-Null
    }

    # Collect per-task data
    $taskData = @()
    foreach ($task in $mapping.tasks) {
        $className = $task.classes[0]
        $category = $task.category
        $fixedFile = if ($task.fixedFile) { Join-Path $RepoRoot $task.fixedFile } else { "" }
        $unsafeFile = if ($task.unsafeFile) { Join-Path $RepoRoot $task.unsafeFile } else { "" }

        $metrics = Get-TaskIterationMetrics -ClassName $className
        $comparison = Compare-TaskFiles -AgentFile $unsafeFile -KnownGoodFile $fixedFile

        $taskData += @{
            ClassName   = $className
            Category    = $category
            Iterations  = $metrics.Iterations
            TotalTests  = $metrics.TotalTests
            PassedTests = $metrics.PassedTests
            FailedTests = $metrics.FailedTests
            PassRate    = $metrics.PassRate
            Success     = $metrics.Success
            Comparison  = $comparison.Reason
            TestMethods = $task.fixedTestMethods.Count
        }
    }

    $summaryMetrics = Get-GroupMetrics -Tasks $taskData

    # Build report sections
    $reportLines = @()
    $reportLines += "# Pipeline Final Report"
    $reportLines += ""
    $reportLines += "Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss UTC' -AsUTC)"
    $reportLines += ""
    $reportLines += Build-SummarySection -Metrics $summaryMetrics
    $reportLines += Build-CategoryBreakdown -TaskData $taskData
    $reportLines += Build-PerTaskTable -TaskData $taskData
    $reportLines += Build-FailedTasksDetail -TaskData $taskData

    # Write report
    $reportFile = Join-Path $ReportsDir "final-report.md"
    $reportLines -join "`n" | Set-Content -Path $reportFile -Encoding utf8 -NoNewline

    Write-Host "`nPhase 6 complete: report written to pipeline/reports/final-report.md" -ForegroundColor Green
    Write-Host "  Total: $($summaryMetrics.Total) | Passed: $($summaryMetrics.Passed) | Failed: $($summaryMetrics.Failed)" -ForegroundColor White

    return $reportFile
}

# ─── Main entry point ──────────────────────────────────────────────────────────

Write-Host "Pipeline: SDK Multithreading Migration" -ForegroundColor White
Write-Host "Repository root: $RepoRoot"

if ($Phase6Only) {
    Invoke-Phase6
} elseif ($Phase3Only) {
    Invoke-Phase3
} elseif ($Phase1Only) {
    Invoke-Phase1
} elseif ($Phase5Only) {
    # Phase 5 placeholder (error analysis and retry)
    Write-Host "`n=== Phase 5: Error Analysis & Retry ===" -ForegroundColor Cyan
    Write-Host "Phase 5 not yet implemented. Skipping to Phase 6." -ForegroundColor Yellow
    Invoke-Phase6
} elseif ($StartPhase -gt 1) {
    # Resume from a specific phase
    Write-Host "Resuming from Phase $StartPhase" -ForegroundColor Yellow
    if ($StartPhase -le 3) {
        Invoke-Phase3
    }
    if ($StartPhase -eq 5) {
        Write-Host "Phase 5 not yet implemented. Skipping to Phase 6." -ForegroundColor Yellow
    }
    if ($StartPhase -le 6) {
        Invoke-Phase6
    }
} else {
    # Full pipeline run: Phase 1 → Phase 3 → Phase 6
    Invoke-Phase1
    Invoke-Phase3
    Invoke-Phase6
}
