# Merge Group 6: COM & Hosting Tasks

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #21 | CreateComHost | `migrate-create-com-host` |
| #22 | FilterResolvedFiles | `migrate-filter-resolved-files` |
| #27 | GenerateRegFreeComManifest | `migrate-generate-reg-free-com-manifest` |
| #28 | GenerateShims | `migrate-generate-shims` |
| #37 | GetPackagesToPrune | `migrate-get-packages-to-prune` |

## Expected Files

**Task source files (modified):**
- `src/Tasks/Microsoft.NET.Build.Tasks/CreateComHost.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/FilterResolvedFiles.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateRegFreeComManifest.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateShims.cs` (⚠️ touched by both PR #28 and #37)
- `src/Tasks/Microsoft.NET.Build.Tasks/ParseTargetManifests.cs` (⚠️ touched by PR #28)
- `src/Tasks/Microsoft.NET.Build.Tasks/GetPackagesToPrune.cs`

**Test files (new):**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACreateComHostMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAFilterResolvedFilesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateRegFreeComManifestMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateShimsMultiThreading.cs` (⚠️ touched by both #28 and #37)
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetPackagesToPruneMultiThreading.cs`

## ⚠️ Critical: Merge Order and Conflicts

**PR #28 (GenerateShims) and PR #37 (GetPackagesToPrune)** both modify:
- `GenerateShims.cs` — both PRs add multithreading support to this file
- `GivenAGenerateShimsMultiThreading.cs` — both PRs modify this test file

**PR #28 also modifies `ParseTargetManifests.cs`** — this file is the primary target of PR #38
(in Group 7). Merge PR #28 first in this group so Group 7 can rebase cleanly.

**Merge order within this group:**
1. PR #21 (CreateComHost) — no conflicts
2. PR #22 (FilterResolvedFiles) — no conflicts
3. PR #27 (GenerateRegFreeComManifest) — no conflicts
4. PR #28 (GenerateShims) — **merge before #37**
5. PR #37 (GetPackagesToPrune) — **will conflict on GenerateShims.cs and test file, resolve manually**

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-6

# Non-conflicting PRs first
git merge --no-ff origin/migrate-create-com-host
git merge --no-ff origin/migrate-filter-resolved-files
git merge --no-ff origin/migrate-generate-reg-free-com-manifest

# GenerateShims first (establishes base for GetPackagesToPrune)
git merge --no-ff origin/migrate-generate-shims

# GetPackagesToPrune last — EXPECT CONFLICTS on GenerateShims.cs
git merge --no-ff origin/migrate-get-packages-to-prune
# Resolve conflicts:
#   - GenerateShims.cs: keep both sets of changes (PR #28's migration + PR #37's changes)
#   - GivenAGenerateShimsMultiThreading.cs: keep the version from PR #37 (it's a superset)

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Migrate 5 MSBuild tasks to IMultiThreadableTask (Group 6)

Migrated tasks:
- CreateComHost
- FilterResolvedFiles
- GenerateRegFreeComManifest
- GenerateShims
- GetPackagesToPrune

Each task receives [MSBuildMultiThreadableTask] attribute, IMultiThreadableTask
interface, TaskEnvironment property, and forbidden API replacements.

Note: GenerateShims and GetPackagesToPrune share code in GenerateShims.cs.
ParseTargetManifests.cs also received minor changes as a dependency of GenerateShims.

Includes multithreading unit tests for each task."
```

## Conflict Notes

| Files | Conflicting PRs | Resolution |
|-------|-----------------|------------|
| `GenerateShims.cs` | #28, #37 | Keep both changes; #28 adds attribute/interface, #37 adds path absolutization |
| `GivenAGenerateShimsMultiThreading.cs` | #28, #37 | Use #37's version (superset) |
| `ParseTargetManifests.cs` | #28 (here), #38 (Group 7) | #28's changes land first; Group 7 rebases on top |

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```

**Important:** This group must be merged **before Group 7** due to the `ParseTargetManifests.cs` dependency.
