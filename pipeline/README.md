# Pipeline

Automation pipeline for the SDK multithreading migration project using the GitHub Copilot CLI agent.

## Prerequisites

- **GitHub Copilot CLI** (`copilot`) v0.0.408 or later installed and on `PATH`
  - Verify: `copilot --version`
- **.NET SDK** matching the version in `global.json`
- The solution builds successfully: `dotnet build ../SdkMultithreadingMigration.slnx`

## Usage

1. Ensure prerequisites are met.
2. Run the full pipeline:
   ```powershell
   ./run-pipeline.ps1
   ```
3. Run individual phases:
   ```powershell
   ./run-pipeline.ps1 -Phase1Only      # Generate prompts only
   ./run-pipeline.ps1 -Phase3Only      # Agent invocation only
   ./run-pipeline.ps1 -StartPhase 6    # Generate final report only
   ```
4. Resume from a specific phase:
   ```powershell
   ./run-pipeline.ps1 -StartPhase 3    # Resume from Phase 3 onward
   ./run-pipeline.ps1 -StartPhase 3 -Iteration 2  # Resume with iteration hint
   ```

## Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-RepoRoot` | string | parent dir | Repository root path |
| `-Phase1Only` | switch | false | Run only Phase 1 (prompt generation) |
| `-Phase3Only` | switch | false | Run only Phase 3 (agent invocation) |
| `-MaxRetries` | int | 5 | Max retry iterations per task in Phase 3 |
| `-StartPhase` | int | 0 | Resume pipeline from this phase (1, 3, or 6) |
| `-Iteration` | int | 1 | Iteration hint for Phase 3 resume |

## Phases

| Phase | Description |
|-------|-------------|
| 1. Analyze | Generate migration prompts from masked tasks in `MaskedTasks/` |
| 3. Migrate & Test | Invoke agent, run tests, retry up to `-MaxRetries` times |
| 6. Report | Generate `reports/final-report.md` comparing agent vs known-good fixed versions |

## Output Structure

```
pipeline/
├── config.json      # Agent and path configuration
├── .gitignore       # Ignores logs/, reports/, *.trx
├── README.md        # This file
├── run-pipeline.ps1 # Pipeline script
├── logs/            # Runtime logs (git-ignored)
└── reports/         # Generated reports (git-ignored)
    └── final-report.md  # Phase 6 output with per-task metrics
```
