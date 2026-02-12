# Pipeline

Automation pipeline for the SDK multithreading migration project using the GitHub Copilot CLI agent.

## Prerequisites

- **GitHub Copilot CLI** (`copilot`) v0.0.408 or later installed and on `PATH`
  - Verify: `copilot --version`
- **.NET SDK** matching the version in `global.json`
- The solution builds successfully: `dotnet build ../SdkMultithreadingMigration.slnx`

## Usage

1. Ensure prerequisites are met.
2. Run the Copilot CLI agent with the configured flags:
   ```
   copilot --yolo --no-ask-user --silent
   ```
3. Prompt files are located in `../skills/`.
4. Logs are written to `logs/` and reports to `reports/` (both git-ignored).

## Phases

| Phase | Description |
|-------|-------------|
| 1. Analyze | Run analysis prompts from `skills/` against the codebase |
| 2. Migrate | Apply migration templates to transform unsafe patterns |
| 3. Test | Build the solution and run tests, collecting `.trx` results |
| 4. Report | Generate summary reports in `reports/` |

## Output Structure

```
pipeline/
├── config.json      # Agent and path configuration
├── .gitignore       # Ignores logs/, reports/, *.trx
├── README.md        # This file
├── logs/            # Runtime logs (git-ignored)
└── reports/         # Generated reports (git-ignored)
```
