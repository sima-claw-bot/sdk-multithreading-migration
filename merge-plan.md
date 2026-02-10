# Merge Plan: MSBuild Multithreading Task Migrations

## Overview

38 open PRs in [SimaTian/sdk](https://github.com/SimaTian/sdk/pulls) need to be consolidated
into groups of ~5 for submission to `dotnet/sdk` for review. Each group will be merged into
a single branch with **one squashed commit** targeting `multithreading-polyfills`.

## PR Inventory (38 open PRs)

| PR | Task | Branch | Type |
|----|------|--------|------|
| #1 | ResolvePackageDependencies | migrate-resolve-package-dependencies | task + test |
| #2 | GetAssemblyAttributes | migrate-getassemblyattributes-multithreading | task + test |
| #3 | GenerateToolsSettingsFile | migrate-generate-tools-settings | task + test |
| #4 | GenerateClsidMap | migrate-generate-clsid-map | task + test + csproj |
| #5 | GenerateRuntimeConfigurationFiles | migrate-generate-runtime-config | task + test |
| #6 | GenerateBundle | migrate-generate-bundle | task + test |
| #7 | GenerateDepsFile | migrate-generate-deps-file | task + test |
| #8 | WriteAppConfigWithSupportedRuntime | migrate-writeappconfigwithsupportedruntime-multithreading | task + test |
| #9 | ResolveAppHosts | migrate-resolve-app-hosts | task + test |
| #10 | ShowPreviewMessage | migrate-show-preview-message | task + test |
| #11 | ResolvePackageAssets | migrate-resolvepackageassets-multithreading | task + test |
| #12 | CreateAppHost | migrate-create-app-host | task + test + csproj |
| #13 | GetDependsOnNETStandard | migrate-get-depends-on-netstandard | Extensions.Tasks (separate project) |
| #14 | AllowEmptyTelemetry | migrate-allow-empty-telemetry | task + test |
| #15 | CheckForDuplicateFrameworkReferences | migrate-check-for-duplicate-framework-references | test only (attribute pre-existing) |
| #16 | CheckForImplicitPackageReferenceOverrides | migrate-check-for-implicit-package-reference-overrides | test only |
| #17 | CheckForTargetInAssetsFile | migrate-check-for-target-in-assets-file | task + test |
| #18 | CheckForUnsupportedWinMDReferences | migrate-check-for-unsupported-winmd-references | task + test |
| #19 | CheckIfPackageReferenceShouldBeFrameworkReference | migrate-check-if-package-reference-should-be-framework-reference | test only |
| #21 | CreateComHost | migrate-create-com-host | task + test |
| #22 | FilterResolvedFiles | migrate-filter-resolved-files | task + test |
| #23 | CollatePackageDownloads | migrate-collate-package-downloads | test only |
| #24 | CollectSDKReferencesDesignTime | migrate-collect-sdkreferences-design-time | test only |
| #25 | CreateWindowsSdkKnownFrameworkReferences | migrate-create-windows-sdk-known-framework-references | test only |
| #26 | FindItemsFromPackages | migrate-find-items-from-packages | test only |
| #27 | GenerateRegFreeComManifest | migrate-generate-reg-free-com-manifest | task + test |
| #28 | GenerateShims | migrate-generate-shims | task + test (⚠️ touches ParseTargetManifests.cs) |
| #30 | GetAssemblyVersion | migrate-get-assembly-version | task + test |
| #31 | GetPackageDirectory | migrate-get-package-directory | task + test |
| #32 | GenerateSupportedTargetFrameworkAlias | migrate-generate-supported-target-framework-alias | test only |
| #33 | GetDefaultPlatformTargetForNetFramework | migrate-get-default-platform-target-for-net-framework | test only |
| #34 | GetEmbeddedApphostPaths | migrate-get-embedded-apphost-paths | test only |
| #35 | GetNuGetShortFolderName | migrate-get-nu-get-short-folder-name | test only |
| #37 | GetPackagesToPrune | migrate-get-packages-to-prune | task + test (⚠️ touches GenerateShims.cs) |
| #38 | ParseTargetManifests | migrate-parse-target-manifests | task + test |
| #39 | PickBestRid | migrate-pick-best-rid | task + test |
| #40 | GetPublishItemsOutputGroupOutputs | migrate-get-publish-items-output-group-outputs | test only |
| #41 | PrepareForReadyToRunCompilation | migrate-prepare-for-ready-to-run-compilation | task + test |

## Known Conflicts

- **PR #4 and #12** both modify `Microsoft.NET.Build.Tasks.UnitTests.csproj` → place in same group
- **PR #28, #37, #38** share `GenerateShims.cs` and `ParseTargetManifests.cs` → place in same group
- **PR #12** contains a stray file `GivenAProcessFrameworkReferencesMultiThreading.cs` → must be removed during merge
- **PR #13** is in a separate project (`Extensions.Tasks`) → keep in its own group to isolate review

## Merge Groups

### Group 1: Core Task Migrations (PRs #1–#5)
First batch of core tasks, including the two that touch `.csproj`.

| PR | Task |
|----|------|
| #1 | ResolvePackageDependencies |
| #2 | GetAssemblyAttributes |
| #3 | GenerateToolsSettingsFile |
| #4 | GenerateClsidMap |
| #5 | GenerateRuntimeConfigurationFiles |

**Merge branch:** `merge-group-1`
**Conflicts:** PR #4 touches `.csproj` — no other PR in this group does.

### Group 2: Bundle, Deps, Config & Hosts (PRs #6–#10)
Build artifact and host resolution tasks.

| PR | Task |
|----|------|
| #6 | GenerateBundle |
| #7 | GenerateDepsFile |
| #8 | WriteAppConfigWithSupportedRuntime |
| #9 | ResolveAppHosts |
| #10 | ShowPreviewMessage |

**Merge branch:** `merge-group-2`
**Conflicts:** None.

### Group 3: Package Assets & App Host (PRs #11–#14, #17)
Package resolution and app host tasks, including Extensions.Tasks.

| PR | Task |
|----|------|
| #11 | ResolvePackageAssets |
| #12 | CreateAppHost |
| #13 | GetDependsOnNETStandard |
| #14 | AllowEmptyTelemetry |
| #17 | CheckForTargetInAssetsFile |

**Merge branch:** `merge-group-3`
**Conflicts:** PR #12 touches `.csproj` — no other PR in this group does. PR #12 has stray file to remove.
**Note:** PR #13 is Extensions.Tasks (separate project). Included here for review proximity.

### Group 4: Validation Checks – Test Only (PRs #15, #16, #18, #19, #23)
Tasks that already had `[MSBuildMultiThreadableTask]` on the base branch; PRs only add multithreading tests.

| PR | Task |
|----|------|
| #15 | CheckForDuplicateFrameworkReferences |
| #16 | CheckForImplicitPackageReferenceOverrides |
| #18 | CheckForUnsupportedWinMDReferences |
| #19 | CheckIfPackageReferenceShouldBeFrameworkReference |
| #23 | CollatePackageDownloads |

**Merge branch:** `merge-group-4`
**Conflicts:** None. All PRs add a single new test file.

### Group 5: Design-Time & SDK Tasks – Test Only (PRs #24, #25, #26, #32, #33)
More test-only PRs for tasks with pre-existing attributes.

| PR | Task |
|----|------|
| #24 | CollectSDKReferencesDesignTime |
| #25 | CreateWindowsSdkKnownFrameworkReferences |
| #26 | FindItemsFromPackages |
| #32 | GenerateSupportedTargetFrameworkAlias |
| #33 | GetDefaultPlatformTargetForNetFramework |

**Merge branch:** `merge-group-5`
**Conflicts:** None. All PRs add a single new test file.

### Group 6: COM & Hosting Tasks (PRs #21, #22, #27, #28, #37)
COM interop, filtering, and shim-related tasks. Groups conflicting PRs together.

| PR | Task |
|----|------|
| #21 | CreateComHost |
| #22 | FilterResolvedFiles |
| #27 | GenerateRegFreeComManifest |
| #28 | GenerateShims |
| #37 | GetPackagesToPrune |

**Merge branch:** `merge-group-6`
**Conflicts:** PR #28 and #37 both touch `GenerateShims.cs` — merge #28 first, then #37.
PR #28 also touches `ParseTargetManifests.cs` — handle before Group 7.

### Group 7: Assembly & Package Metadata (PRs #30, #31, #38, #39, #41)
Assembly version, package directory, manifest parsing, RID selection, and R2R.

| PR | Task |
|----|------|
| #30 | GetAssemblyVersion |
| #31 | GetPackageDirectory |
| #38 | ParseTargetManifests |
| #39 | PickBestRid |
| #41 | PrepareForReadyToRunCompilation |

**Merge branch:** `merge-group-7`
**Conflicts:** PR #38 touches `ParseTargetManifests.cs` (also touched by #28 in Group 6).
Group 6 must be merged first, or rebase #38 on top of Group 6's result.

### Group 8: Remaining Test-Only & Publish (PRs #34, #35, #40)
Final group — remaining test-only PRs.

| PR | Task |
|----|------|
| #34 | GetEmbeddedApphostPaths |
| #35 | GetNuGetShortFolderName |
| #40 | GetPublishItemsOutputGroupOutputs |

**Merge branch:** `merge-group-8`
**Conflicts:** None. All PRs add a single new test file.

## Merge Order

Groups must be merged in order due to file conflicts:

```
Group 1 ──┐
Group 2 ──┤
Group 3 ──┤  (independent, can be parallel)
Group 4 ──┤
Group 5 ──┘
           │
Group 6 ───┐  (must come before Group 7 due to ParseTargetManifests.cs / GenerateShims.cs)
Group 7 ───┘
           │
Group 8 ───── (independent, can go anytime)
```

## Merge Procedure (per group)

1. Create a new branch `merge-group-N` from `multithreading-polyfills`
2. For each PR in the group, merge its branch: `git merge --no-ff <branch>`
3. Resolve any conflicts (expected only in Groups 3, 6, 7)
4. After all PRs merged, squash into a single commit: `git reset --soft multithreading-polyfills && git commit`
5. Build and run unit tests to verify
6. Push and create PR against `dotnet/sdk` `multithreading-polyfills` branch
7. Close the individual SimaTian/sdk PRs with reference to the group PR

## Prompt Files

See `merge-prompts/` directory for individual merge prompt files:
- `merge-group-1.md` through `merge-group-8.md`

Each prompt file contains the exact branches, merge order, conflict notes,
expected files, commit message, and verification steps for that group.
