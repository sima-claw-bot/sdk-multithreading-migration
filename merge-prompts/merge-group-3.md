# Merge Group 3: Package Assets & App Host

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #11 | ResolvePackageAssets | `migrate-resolvepackageassets-multithreading` |
| #12 | CreateAppHost | `migrate-create-app-host` |
| #13 | GetDependsOnNETStandard | `migrate-get-depends-on-netstandard` |
| #14 | AllowEmptyTelemetry | `migrate-allow-empty-telemetry` |
| #17 | CheckForTargetInAssetsFile | `migrate-check-for-target-in-assets-file` |

## Expected Files

**Task source files (modified):**
- `src/Tasks/Microsoft.NET.Build.Tasks/ResolvePackageAssets.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/CreateAppHost.cs`
- `src/Tasks/Microsoft.NET.Build.Extensions.Tasks/GetDependsOnNETStandard.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/AllowEmptyTelemetry.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/CheckForTargetInAssetsFile.cs`

**Test files (new):**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAResolvePackageAssetsMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACreateAppHostMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Extensions.Tasks.UnitTests/GivenAGetDependsOnNETStandardMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAAllowEmptyTelemetryMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACheckForTargetInAssetsFileMultiThreading.cs`

**Other modified files:**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj` (from PR #12)
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAResolvePackageAssetsTask.cs` (from PR #11)
- `src/Tasks/Microsoft.NET.Build.Extensions.Tasks.UnitTests/GivenAGetDependsOnNETStandardTask.cs` (from PR #13)
- `src/Tasks/Microsoft.NET.Build.Extensions.Tasks.UnitTests/Microsoft.NET.Build.Extensions.Tasks.UnitTests.csproj` (from PR #13)

## ⚠️ Special Handling

**PR #12 contains a stray file:** `GivenAProcessFrameworkReferencesMultiThreading.cs`
This file belongs to a different task and must be **removed** after merging PR #12:

```bash
git rm src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAProcessFrameworkReferencesMultiThreading.cs
```

**PR #13 is in a separate project** (`Microsoft.NET.Build.Extensions.Tasks`). It modifies:
- A different task project (`Extensions.Tasks` instead of `Build.Tasks`)
- A different test project (`Extensions.Tasks.UnitTests`)
- Its own `.csproj` file (adding `TaskEnvironmentHelper` compile link)

This does not conflict with other PRs but reviewers should note the different project scope.

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-3

git merge --no-ff origin/migrate-resolvepackageassets-multithreading
git merge --no-ff origin/migrate-create-app-host
# Remove stray file from PR #12
git rm src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAProcessFrameworkReferencesMultiThreading.cs
git commit -m "Remove stray ProcessFrameworkReferences test file from CreateAppHost PR"
git merge --no-ff origin/migrate-get-depends-on-netstandard
git merge --no-ff origin/migrate-allow-empty-telemetry
git merge --no-ff origin/migrate-check-for-target-in-assets-file

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Migrate 5 MSBuild tasks to IMultiThreadableTask (Group 3)

Migrated tasks:
- ResolvePackageAssets
- CreateAppHost
- GetDependsOnNETStandard (Extensions.Tasks project)
- AllowEmptyTelemetry
- CheckForTargetInAssetsFile

Each task receives [MSBuildMultiThreadableTask] attribute, IMultiThreadableTask
interface, TaskEnvironment property, and forbidden API replacements.

Note: GetDependsOnNETStandard is in the Microsoft.NET.Build.Extensions.Tasks
project (separate from the main Build.Tasks project).

Includes multithreading unit tests for each task."
```

## Conflict Notes

PR #12 modifies `.csproj` — no other PR in this group does, so no conflict.
PR #13 is in a different project entirely — no overlap.

## Verification

```bash
# Main tasks
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build

# Extensions tasks
dotnet build src/Tasks/Microsoft.NET.Build.Extensions.Tasks.UnitTests/Microsoft.NET.Build.Extensions.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Extensions.Tasks.UnitTests/Microsoft.NET.Build.Extensions.Tasks.UnitTests.csproj --no-build
```
