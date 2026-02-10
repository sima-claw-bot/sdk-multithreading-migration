# Merge Group 7: Assembly & Package Metadata

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #30 | GetAssemblyVersion | `migrate-get-assembly-version` |
| #31 | GetPackageDirectory | `migrate-get-package-directory` |
| #38 | ParseTargetManifests | `migrate-parse-target-manifests` |
| #39 | PickBestRid | `migrate-pick-best-rid` |
| #41 | PrepareForReadyToRunCompilation | `migrate-prepare-for-ready-to-run-compilation` |

## Dependency

⚠️ **This group depends on Group 6 being merged first.**

PR #38 (ParseTargetManifests) modifies `ParseTargetManifests.cs`, which was also touched by
PR #28 (GenerateShims) in Group 6. The merge-group-6 branch must be merged into
`multithreading-polyfills` before starting this group, or this group's branch must be
based on `merge-group-6` instead.

## Expected Files

**Task source files (modified):**
- `src/Tasks/Microsoft.NET.Build.Tasks/GetAssemblyVersion.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GetPackageDirectory.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/ParseTargetManifests.cs` (⚠️ also touched by Group 6)
- `src/Tasks/Microsoft.NET.Build.Tasks/PickBestRid.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/PrepareForReadyToRunCompilation.cs`

**Test files (new):**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetAssemblyVersionMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetPackageDirectoryMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAParseTargetManifestsMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAPickBestRidMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAPrepareForReadyToRunCompilationMultiThreading.cs`

## Merge Procedure

```bash
# Ensure Group 6 is already merged into multithreading-polyfills
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-7

git merge --no-ff origin/migrate-get-assembly-version
git merge --no-ff origin/migrate-get-package-directory
git merge --no-ff origin/migrate-parse-target-manifests
# If ParseTargetManifests.cs conflicts (due to Group 6 changes), resolve by keeping
# both Group 6's changes and PR #38's migration additions.
git merge --no-ff origin/migrate-pick-best-rid
git merge --no-ff origin/migrate-prepare-for-ready-to-run-compilation

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Migrate 5 MSBuild tasks to IMultiThreadableTask (Group 7)

Migrated tasks:
- GetAssemblyVersion
- GetPackageDirectory
- ParseTargetManifests
- PickBestRid
- PrepareForReadyToRunCompilation

Each task receives [MSBuildMultiThreadableTask] attribute, IMultiThreadableTask
interface, TaskEnvironment property, and forbidden API replacements.

Includes multithreading unit tests for each task."
```

## Conflict Notes

| File | Conflict Source | Resolution |
|------|----------------|------------|
| `ParseTargetManifests.cs` | Group 6 (PR #28) already modified this file | Rebase PR #38 changes on top of Group 6's version |

All other PRs in this group touch independent files.

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```
