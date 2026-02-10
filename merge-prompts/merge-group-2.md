# Merge Group 2: Bundle, Deps, Config & Hosts

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #6 | GenerateBundle | `migrate-generate-bundle` |
| #7 | GenerateDepsFile | `migrate-generate-deps-file` |
| #8 | WriteAppConfigWithSupportedRuntime | `migrate-writeappconfigwithsupportedruntime-multithreading` |
| #9 | ResolveAppHosts | `migrate-resolve-app-hosts` |
| #10 | ShowPreviewMessage | `migrate-show-preview-message` |

## Expected Files

**Task source files (modified):**
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateBundle.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/GenerateDepsFile.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/WriteAppConfigWithSupportedRuntime.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/ResolveAppHosts.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks/ShowPreviewMessage.cs`

**Test files (new):**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateBundleMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateDepsFileMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAWriteAppConfigWithSupportedRuntimeMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAResolveAppHostsMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAShowPreviewMessageMultiThreading.cs`

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-2

git merge --no-ff origin/migrate-generate-bundle
git merge --no-ff origin/migrate-generate-deps-file
git merge --no-ff origin/migrate-writeappconfigwithsupportedruntime-multithreading
git merge --no-ff origin/migrate-resolve-app-hosts
git merge --no-ff origin/migrate-show-preview-message

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Migrate 5 MSBuild tasks to IMultiThreadableTask (Group 2)

Migrated tasks:
- GenerateBundle
- GenerateDepsFile
- WriteAppConfigWithSupportedRuntime
- ResolveAppHosts
- ShowPreviewMessage

Each task receives [MSBuildMultiThreadableTask] attribute, IMultiThreadableTask
interface, TaskEnvironment property, and forbidden API replacements.

Includes multithreading unit tests for each task."
```

## Conflict Notes

No conflicts expected. All PRs touch completely independent files.

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```
