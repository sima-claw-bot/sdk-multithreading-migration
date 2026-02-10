# Merge Group 8: Remaining Test-Only & Publish

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #34 | GetEmbeddedApphostPaths | `migrate-get-embedded-apphost-paths` |
| #35 | GetNuGetShortFolderName | `migrate-get-nu-get-short-folder-name` |
| #40 | GetPublishItemsOutputGroupOutputs | `migrate-get-publish-items-output-group-outputs` |

## Expected Files

**Test files only (new) — no task source changes needed:**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetEmbeddedApphostPathsMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetNuGetShortFolderNameMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetPublishItemsOutputGroupOutputsMultiThreading.cs`

**Note:** These 3 tasks already had `[MSBuildMultiThreadableTask]` attribute and `IMultiThreadableTask`
interface on the `multithreading-polyfills` base branch. The PRs only add multithreading stress tests.

This group has only 3 PRs (not 5) as it contains the remaining tasks after filling the other groups.

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-8

git merge --no-ff origin/migrate-get-embedded-apphost-paths
git merge --no-ff origin/migrate-get-nu-get-short-folder-name
git merge --no-ff origin/migrate-get-publish-items-output-group-outputs

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Add multithreading tests for 3 pre-migrated tasks (Group 8)

Tasks (attribute already present on base branch):
- GetEmbeddedApphostPaths
- GetNuGetShortFolderName
- GetPublishItemsOutputGroupOutputs

These tasks already had [MSBuildMultiThreadableTask] and IMultiThreadableTask.
This commit adds multithreading unit tests to verify thread safety."
```

## Conflict Notes

No conflicts expected. Each PR adds a single new test file with no overlap.
This group is independent and can be merged at any point in the sequence.

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```
