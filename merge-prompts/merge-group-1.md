# Merge Group 1: Core Task Migrations

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #1 | ResolvePackageDependencies | `migrate-resolve-package-dependencies` |
| #2 | GetAssemblyAttributes | `migrate-getassemblyattributes-multithreading` |
| #3 | GenerateToolsSettingsFile | `migrate-generate-tools-settings` |
| #4 | GenerateClsidMap | `migrate-generate-clsid-map` |
| #5 | GenerateRuntimeConfigurationFiles | `migrate-generate-runtime-config` |

## Expected Files

**Task source files (modified):**
- `src/Tasks/Microsoft.NET.Build.Tasks/ResolvePackageDependencies.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GetAssemblyAttributes.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateToolsSettingsFile.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateClsidMap.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateRuntimeConfigurationFiles.cs`

**Test files (new):**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAResolvePackageDependenciesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetAssemblyAttributesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateToolsSettingsFileMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateClsidMapMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateRuntimeConfigMultiThreading.cs`

**Other modified files:**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj` (from PR #4)
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAResolvePackageDependenciesTask.cs` (from PR #1)
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateRuntimeConfigurationFiles.cs` (from PR #5)

## Merge Procedure

```bash
# Start from the base branch
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-1

# Merge each PR branch in order
git merge --no-ff origin/migrate-resolve-package-dependencies
git merge --no-ff origin/migrate-getassemblyattributes-multithreading
git merge --no-ff origin/migrate-generate-tools-settings
git merge --no-ff origin/migrate-generate-clsid-map
git merge --no-ff origin/migrate-generate-runtime-config

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Migrate 5 MSBuild tasks to IMultiThreadableTask (Group 1)

Migrated tasks:
- ResolvePackageDependencies
- GetAssemblyAttributes
- GenerateToolsSettingsFile
- GenerateClsidMap
- GenerateRuntimeConfigurationFiles

Each task receives [MSBuildMultiThreadableTask] attribute, IMultiThreadableTask
interface, TaskEnvironment property, and forbidden API replacements (path
absolutization for File/Directory/FileStream operations).

Includes multithreading unit tests for each task."
```

## Conflict Notes

No conflicts expected between PRs in this group. PR #4 is the only one modifying `.csproj`.

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```

All multithreading tests should pass. Expect ~11 pre-existing test failures (unrelated `redist.csproj` issue).
