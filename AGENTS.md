# AGENTS.md — Auto-generated Task Instructions

## Current Task
**task-1 Scaffold .NET solution and verify build environment**

> Part of #14

## Description
Run `dotnet --version` to confirm SDK (10.0.103 available, target net10.0). Create `global.json` pinning SDK 10.0.103. Create `Directory.Build.props` (suppress NU1903). Run `dotnet new sln -n SdkMultithreadingMigration`. Create `SharedPolyfills/SharedPolyfills.csproj` (net10.0, refs Microsoft.Build.Framework 17.* + Microsoft.Build.Utilities.Core 17.* using version ranges), `UnsafeThreadSafeTasks/UnsafeThreadSafeTasks.csproj` (net10.0, same MSBuild refs, ProjectReference to SharedPolyfills), `FixedThreadSafeTasks/FixedThreadSafeTasks.csproj` (same shape), and `UnsafeThreadSafeTasks.Tests/UnsafeThreadSafeTasks.Tests.csproj` (net10.0, xunit 2.9.3, Microsoft.NET.Test.Sdk 17.12.0, xunit.runner.visualstudio 2.8.2, ProjectReference to UnsafeThreadSafeTasks, FixedThreadSafeTasks, and SharedPolyfills). Run `dotnet sln add` for all projects. Create subdirectory stubs under both UnsafeThreadSafeTasks and FixedThreadSafeTasks for PathViolations, EnvironmentViolations, ProcessViolations, ConsoleViolations, SubtleViolations, ComplexViolations, IntermittentViolations, MismatchViolations. Verify `dotnet build` succeeds. [critical]

## Metadata
**Task ID:** `task-1`

*Created by agent-orchestrator planning pipeline*

## Working Rules
- Complete the task described above
- Run tests before finishing
- Do not modify files outside the scope of this task
- Commit your changes with a clear commit message


