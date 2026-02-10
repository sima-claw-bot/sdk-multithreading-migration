# Merge Group 4: Validation Checks – Test Only

## PRs in This Group

| PR | Task | Branch |
|----|------|--------|
| #15 | CheckForDuplicateFrameworkReferences | `migrate-check-for-duplicate-framework-references` |
| #16 | CheckForImplicitPackageReferenceOverrides | `migrate-check-for-implicit-package-reference-overrides` |
| #18 | CheckForUnsupportedWinMDReferences | `migrate-check-for-unsupported-winmd-references` |
| #19 | CheckIfPackageReferenceShouldBeFrameworkReference | `migrate-check-if-package-reference-should-be-framework-reference` |
| #23 | CollatePackageDownloads | `migrate-collate-package-downloads` |

## Expected Files

**Test files only (new) — no task source changes needed:**
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACheckForDuplicateFrameworkReferencesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACheckForImplicitPackageReferenceOverridesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACheckForUnsupportedWinMDReferencesMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACheckIfPackageReferenceShouldBeFrameworkReferenceMultiThreading.cs`
- `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/GivenACollatePackageDownloadsMultiThreading.cs`

**Note:** These 5 tasks already had `[MSBuildMultiThreadableTask]` attribute and `IMultiThreadableTask`
interface on the `multithreading-polyfills` base branch. The PRs only add multithreading stress tests.

However, PR #18 (CheckForUnsupportedWinMDReferences) also modifies the task source file — verify
this change is correct and not an accidental leftover.

## Merge Procedure

```bash
git checkout multithreading-polyfills
git pull origin multithreading-polyfills
git checkout -b merge-group-4

git merge --no-ff origin/migrate-check-for-duplicate-framework-references
git merge --no-ff origin/migrate-check-for-implicit-package-reference-overrides
git merge --no-ff origin/migrate-check-for-unsupported-winmd-references
git merge --no-ff origin/migrate-check-if-package-reference-should-be-framework-reference
git merge --no-ff origin/migrate-collate-package-downloads

# Squash into a single commit
git reset --soft multithreading-polyfills
git commit -m "Add multithreading tests for 5 pre-migrated tasks (Group 4)

Tasks (attribute already present on base branch):
- CheckForDuplicateFrameworkReferences
- CheckForImplicitPackageReferenceOverrides
- CheckForUnsupportedWinMDReferences
- CheckIfPackageReferenceShouldBeFrameworkReference
- CollatePackageDownloads

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
