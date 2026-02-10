# Merge Group 5: Design-Time & SDK Tasks – Test Only

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #24 | CollectSDKReferencesDesignTime | `migrate-collect-sdkreferences-design-time` |
| #25 | CreateWindowsSdkKnownFrameworkReferences | `migrate-create-windows-sdk-known-framework-references` |
| #26 | FindItemsFromPackages | `migrate-find-items-from-packages` |
| #32 | GenerateSupportedTargetFrameworkAlias | `migrate-generate-supported-target-framework-alias` |
| #33 | GetDefaultPlatformTargetForNetFramework | `migrate-get-default-platform-target-for-net-framework` |

## Expected Files

**Test files only (new) — no task source changes needed:**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACollectSDKReferencesDesignTimeMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACreateWindowsSdkKnownFrameworkReferencesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAFindItemsFromPackagesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGenerateSupportedTargetFrameworkAliasMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenAGetDefaultPlatformTargetForNetFrameworkMultiThreading.cs`

**Note:** These 5 tasks already had `[MSBuildMultiThreadableTask]` attribute and `IMultiThreadableTask`
interface on the `multithreading-polyfills` base branch. The PRs only add multithreading stress tests.

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-5

git merge --no-ff origin/migrate-collect-sdkreferences-design-time
git merge --no-ff origin/migrate-create-windows-sdk-known-framework-references
git merge --no-ff origin/migrate-find-items-from-packages
git merge --no-ff origin/migrate-generate-supported-target-framework-alias
git merge --no-ff origin/migrate-get-default-platform-target-for-net-framework

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Add multithreading tests for 5 pre-migrated tasks (Group 5)

Tasks (attribute already present on base branch):
- CollectSDKReferencesDesignTime
- CreateWindowsSdkKnownFrameworkReferences
- FindItemsFromPackages
- GenerateSupportedTargetFrameworkAlias
- GetDefaultPlatformTargetForNetFramework

These tasks already had [MSBuildMultiThreadableTask] and IMultiThreadableTask.
This commit adds multithreading unit tests to verify thread safety."
```

## Conflict Notes

No conflicts expected. Each PR adds a single new test file with no overlap.

## Verification

```bash
dotnet build src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj
dotnet test src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/Microsoft.NET.Build.Tasks.UnitTests.csproj --no-build
```
