# MSBuild Multithreading Migration Plan

## Repository & Branch
- **Code repo**: https://github.com/SimaTian/sdk (fork of dotnet/sdk)
- **Work branch**: `multithreading-polyfills` — polyfill infrastructure (merged/ready)
- **Planning repo**: https://github.com/SimaTian/sdk-multithreading-migration (this repo)

## Progress
- [x] **Prompt 00 — Polyfill Setup** — DONE (branch `multithreading-polyfills`)
- [ ] Prompts 01–13 — Pre-analyzed interface-based migrations (pending)
- [ ] Prompts 14–52 — Individual task migrations needing analysis (pending)

## Prompt & Skill Files

### Skills (shared knowledge — 3 files):
- `skills/multithreaded-task-migration.md` — Full context: repo layout, API reference, forbidden APIs, test patterns
- `skills/interface-migration-template.md` — Step-by-step TDD template for interface-based migrations
- `skills/analyze-and-migrate-template.md` — TDD analysis-first process for tasks not yet analyzed

### Prompts (53 total — one per work item):
- `prompts/00-polyfill-setup.md` — ✅ DONE — Created polyfills in `src/Tasks/Common/`
- `prompts/01-13-*.md` — Pre-analyzed interface-based migrations (detailed forbidden API analysis included)
- `prompts/14-52-*.md` — Individual task migrations (agent analyzes each task for forbidden APIs and applies correct approach)

### Implementation Notes (from Prompt 00 execution):
- **Test project targets `$(SdkTargetFramework)` (net11.0)**, NOT net472 — polyfills are `#if NETFRAMEWORK` only
- **MSBuild Framework 18.4.0-preview** (used by net11.0 target) already has real `IMultiThreadableTask`, `TaskEnvironment`, `AbsolutePath`, `ITaskEnvironmentDriver`
- **`TaskEnvironment` constructor is internal**, `ITaskEnvironmentDriver` is internal — test helper uses `DispatchProxy` + reflection
- **`TestDriverProxy`** stores its own `ProjectDirectory` independent from process CWD, enabling TDD tests
- 11 pre-existing test failures (need `redist.csproj` built) — unrelated to migration work

## Problem Statement

Migrate tasks in `src/Tasks/Microsoft.NET.Build.Tasks` and `src/Tasks/Microsoft.NET.Build.Extensions.Tasks` to support MSBuild multithreaded execution. Tasks must either:
- Use `[MSBuildMultiThreadableTask]` attribute (for tasks that don't touch global process state) — **most of these are already done**
- Implement `IMultiThreadableTask` interface + attribute (for tasks using forbidden APIs like `Path.GetFullPath()`, `Environment.GetEnvironmentVariable()`, `File.*` with relative paths, `Process.Start`, etc.)

Reference: [Thread-Safe Tasks Spec](https://github.com/dotnet/msbuild/blob/d58f712998dc831d3e3adcdb30ede24f6424348d/documentation/specs/multithreading/thread-safe-tasks.md) and [Migration Skill](https://github.com/dotnet/msbuild/blob/d58f712998dc831d3e3adcdb30ede24f6424348d/.github/skills/multithreaded-task-migration/SKILL.md)

## Approach

1. **Create polyfills** for `IMultiThreadableTask`, `TaskEnvironment`, `AbsolutePath`, and test helper `TaskEnvironmentHelper` (the attribute polyfill already exists in `src/Tasks/Common/MSBuildMultiThreadableTaskAttribute.cs`)
2. **Migrate one task at a time** — each task gets: attribute + interface (if needed), forbidden API replacement, unit tests
3. **Unit tests must be designed to fail on improperly-migrated tasks** (e.g., tests that verify the task uses `TaskEnvironment` instead of global APIs)
4. **Build + existing tests must pass after each task migration**

## Current State Analysis

### Already Migrated (attribute-only, no global state issues):
- `AddPackageType` ✅
- `ApplyImplicitVersions` ✅
- `CheckForDuplicateItems` ✅
- `CheckForDuplicateItemMetadata` ✅ (likely)
- `GenerateGlobalUsings` ✅
- `JoinItems` ✅
- `AddFacadesToReferences` (Extensions.Tasks) ✅

### Need Interface-Based Migration (use forbidden APIs):

#### Microsoft.NET.Build.Tasks — Tasks using `Path.GetFullPath()`:
1. **GetAssemblyAttributes** — `Path.GetFullPath(PathToTemplateFile)` used twice
2. **ResolvePackageDependencies** — `Path.GetFullPath(Path.Combine(...))` in `GetAbsolutePathFromProjectRelativePath()`

#### Microsoft.NET.Build.Tasks — Tasks using file APIs with potentially relative paths:
3. **GenerateClsidMap** — `new FileStream(IntermediateAssembly, ...)` — path might be relative
4. **GenerateToolsSettingsFile** — `.Save(ToolsSettingsFilePath)` — path might be relative
5. **GenerateDepsFile** — `File.Create(depsFilePath)` — path could be relative
6. **GenerateRuntimeConfigurationFiles** — `File.Create(RuntimeConfigPath)`, `File.OpenText(UserRuntimeConfig)`, `File.Exists(UserRuntimeConfig)`
7. **WriteAppConfigWithSupportedRuntime** — `new FileStream(OutputAppConfigFile.ItemSpec, ...)`, `XDocument.Load(appConfigItem.ItemSpec)`
8. **CreateAppHost** — `HostWriter.CreateAppHost(...)` with path params
9. **CreateComHost** — likely same pattern as CreateAppHost
10. **GenerateBundle** — `OutputDir`, `FilesToBundle[].ItemSpec` used in file ops
11. **GenerateRegFreeComManifest** — likely file operations with paths
12. **GenerateShims** — likely file operations with paths
13. **RunReadyToRunCompiler** (ToolTask) — `File.Exists(...)` with paths, `Directory.CreateDirectory(...)`, `Path.GetDirectoryName(...)`
14. **ResolvePackageAssets** — large task, uses `File.Exists`, `File.Open`, `Path.Combine` extensively with potentially relative paths

#### Tasks using `BuildEngine4.GetRegisteredTaskObject` / `RegisterTaskObject` (shared state):
15. **ShowPreviewMessage** — uses `BuildEngine4.GetRegisteredTaskObject` (shared state across builds)
16. **AllowEmptyTelemetry** — uses `BuildEngine as IBuildEngine5` for telemetry

#### Tasks using `Environment.*`:
17. **ValidateExecutableReferences** — uses `BuildEngine6.GetGlobalProperties()` (need to verify if this is safe)

#### Tasks not yet attributed (need analysis):
18. **CheckForDuplicateFrameworkReferences** — needs attribute at minimum
19. **CheckForImplicitPackageReferenceOverrides** — needs attribute at minimum
20. **CheckForTargetInAssetsFile** — needs attribute at minimum
21. **CheckForUnsupportedWinMDReferences** — needs attribute at minimum
22. **CheckIfPackageReferenceShouldBeFrameworkReference** — needs attribute at minimum
23. **CollatePackageDownloads** — needs attribute at minimum
24. **CollectSDKReferencesDesignTime** — needs attribute at minimum
25. **CreateWindowsSdkKnownFrameworkReferences** — needs attribute at minimum
26. **FilterResolvedFiles** — needs attribute at minimum
27. **FindItemsFromPackages** — needs attribute at minimum
28. **FrameworkReferenceResolver** — may use `Path.GetFullPath`, needs analysis
29. **GenerateSupportedTargetFrameworkAlias** — needs attribute at minimum
30. **GetAssemblyVersion** — may use file APIs
31. **GetDefaultPlatformTargetForNetFramework** — needs attribute at minimum
32. **GetEmbeddedApphostPaths** — needs attribute at minimum
33. **GetNuGetShortFolderName** — needs attribute at minimum
34. **GetPackageDirectory** — needs attribute at minimum
35. **GetPackagesToPrune** — needs attribute at minimum
36. **GetPublishItemsOutputGroupOutputs** — needs attribute at minimum
37. **ParseTargetManifests** — needs attribute at minimum
38. **PickBestRid** — needs attribute at minimum
39. **PrepareForReadyToRunCompilation** — may use file APIs
40. **ProcessFrameworkReferences** — needs attribute at minimum
41. **ProduceContentAssets** — file operations likely
42. **RemoveDuplicatePackageReferences** — needs attribute at minimum
43. **ResolveAppHosts** — uses `Path.Combine`, `Directory.Exists`
44. **ResolveCopyLocalAssets** — needs attribute at minimum
45. **ResolveFrameworkReferences** — needs analysis
46. **ResolveReadyToRunCompilers** — needs analysis
47. **ResolveRuntimePackAssets** — needs analysis
48. **ResolveTargetingPackAssets** — needs analysis
49. **RunCsWinRTGenerator** — likely ToolTask subclass
50. **SelectRuntimeIdentifierSpecificItems** — needs attribute at minimum
51. **SetGeneratedAppConfigMetadata** — needs attribute at minimum
52. **ShowMissingWorkloads** — needs attribute at minimum

#### Microsoft.NET.Build.Extensions.Tasks:
53. **GetDependsOnNETStandard** — uses `File.Exists(referenceSourcePath)` with potentially relative paths

## Todo List (Work Items)

### Phase 0: Infrastructure
- **polyfill-setup**: Create polyfills for `IMultiThreadableTask`, `TaskEnvironment`, `AbsolutePath`, `ITaskEnvironmentDriver`, and `TaskEnvironmentHelper` in `src/Tasks/Common/` (gated behind `#if NETFRAMEWORK` like the attribute polyfill). Also create a test helper for `TaskEnvironmentHelper.CreateForTest()`.

### Phase 1: Tasks requiring IMultiThreadableTask (use forbidden APIs — most impactful)

Each task below is a separate work item. For each:
1. Analyze the task for all forbidden API usage (trace all paths)
2. Add `[MSBuildMultiThreadableTask]` attribute + implement `IMultiThreadableTask`
3. Replace forbidden APIs with `TaskEnvironment` equivalents
4. Write thread-safety unit tests that fail without the migration
5. Ensure build passes and existing tests still pass

- **migrate-get-assembly-attributes**: `GetAssemblyAttributes.cs` — replace `Path.GetFullPath()` calls with `TaskEnvironment.GetAbsolutePath()`
- **migrate-resolve-package-dependencies**: `ResolvePackageDependencies.cs` — replace `Path.GetFullPath()` in `GetAbsolutePathFromProjectRelativePath()`
- **migrate-generate-clsid-map**: `GenerateClsidMap.cs` — absolutize `IntermediateAssembly` and `ClsidMapDestinationPath`
- **migrate-generate-tools-settings**: `GenerateToolsSettingsFile.cs` — absolutize `ToolsSettingsFilePath`
- **migrate-generate-deps-file**: `GenerateDepsFile.cs` — absolutize `DepsFilePath`, `ProjectPath`, `AssetsFilePath`
- **migrate-generate-runtime-config**: `GenerateRuntimeConfigurationFiles.cs` — absolutize `RuntimeConfigPath`, `RuntimeConfigDevPath`, `UserRuntimeConfig`
- **migrate-write-app-config**: `WriteAppConfigWithSupportedRuntime.cs` — absolutize `OutputAppConfigFile.ItemSpec`, `AppConfigFile.ItemSpec`
- **migrate-create-app-host**: `CreateAppHost.cs` — absolutize `AppHostSourcePath`, `AppHostDestinationPath`, `IntermediateAssembly`
- **migrate-generate-bundle**: `GenerateBundle.cs` — absolutize `OutputDir`, file paths in `FilesToBundle`
- **migrate-resolve-app-hosts**: `ResolveAppHosts.cs` — absolutize paths in `Path.Combine`, `Directory.Exists` calls
- **migrate-show-preview-message**: `ShowPreviewMessage.cs` — analyze shared state safety with `BuildEngine4`
- **migrate-resolve-package-assets**: `ResolvePackageAssets.cs` — large task, extensive file operations need path absolutization
- **migrate-get-depends-on-netstandard**: `GetDependsOnNETStandard.cs` (Extensions.Tasks) — absolutize paths in `File.Exists`

### Phase 2: Attribute-only tasks (simpler — just add attribute, verify no global state)

These tasks likely need only the `[MSBuildMultiThreadableTask]` attribute and a quick verification that they don't use forbidden APIs. Each still needs a basic thread-safety test.

- **batch-attribute-tasks**: Add `[MSBuildMultiThreadableTask]` to all remaining tasks that only do in-memory transformations (no file I/O, no env vars, no Path.GetFullPath). Batch these in groups of ~5 tasks per work item since they're simpler.

### Phase 3: ToolTask subclasses (deferred complexity)
- **migrate-run-ready-to-run-compiler**: `RunReadyToRunCompiler.cs` — extends `ToolTask`, needs different approach
- **migrate-run-cswinrt-generator**: `RunCsWinRTGenerator.cs` — extends ToolTask

## Notes & Considerations

- The `MSBuildMultiThreadableTaskAttribute` polyfill already exists at `src/Tasks/Common/MSBuildMultiThreadableTaskAttribute.cs` (gated `#if NETFRAMEWORK`)
- `TaskBase` is the common base class in `src/Tasks/Common/TaskBase.cs` — extending from `Task`
- For interface-based tasks: they keep `TaskBase` (or `Task`) as base class and additionally implement `IMultiThreadableTask`
- Tests live in `src/Tasks/Microsoft.NET.Build.Tasks.UnitTests/` with `MockBuildEngine` in `Mocks/MockBuildEngine.cs`
- Test project uses xUnit + FluentAssertions (AwesomeAssertions)
- Polyfills need to handle the case where `IMultiThreadableTask` / `TaskEnvironment` are NOT available in the MSBuild Framework package — use `#if NETFRAMEWORK` conditionals
- Test helper `TaskEnvironmentHelper.CreateForTest()` needs to create a `TaskEnvironment` with a real driver that wraps `Environment.*` and `Directory.GetCurrentDirectory()`
- Thread-safety tests should: (a) run the task from multiple threads with distinct project dirs and verify correct path resolution, (b) verify the task implements `IMultiThreadableTask`, (c) verify TaskEnvironment is used (not raw Path.GetFullPath)
