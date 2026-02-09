# Skill: Stress Testing Multithreaded Task Migrations

## Purpose

After migrating a task to `IMultiThreadableTask`, run stress tests to verify the migration is correct under concurrent execution. These tests are exploratory — not committed to the SDK repo — but are essential for validating that path resolution, instance state, and shared resources are thread-safe.

## Key Findings from ResolvePackageDependencies Migration

### 1. TaskEnvironment null-safety is mandatory

MSBuild only sets `TaskEnvironment` when running in multithreaded mode. In single-threaded mode it's `null`. Every migrated method that uses `TaskEnvironment` must have a null-check fallback to the original behavior:

```csharp
private string GetAbsolutePathFromProjectRelativePath(string path)
{
    string projectDir = Path.GetDirectoryName(ProjectPath);
    if (TaskEnvironment != null)
    {
        AbsolutePath absProjectDir = TaskEnvironment.GetAbsolutePath(projectDir);
        return Path.GetFullPath(Path.Combine(absProjectDir, path));
    }
    return Path.GetFullPath(Path.Combine(projectDir, path));
}
```

**Without this**: existing tests and single-threaded builds will crash with `NullReferenceException`.

### 2. Absolute paths pass through GetAbsolutePath unchanged

`TaskEnvironment.GetAbsolutePath()` checks `Path.IsPathRooted()` — if the path is already absolute, it returns it as-is. This means:
- When `ProjectPath` is absolute (the normal case), `Path.GetDirectoryName(ProjectPath)` is already absolute, so `GetAbsolutePath` is a no-op for path resolution — but it's still required for the contract.
- The real value of `GetAbsolutePath` shows when `ProjectPath` is relative (edge case but possible).

### 3. Instance state is per-task-instance (safe by design)

MSBuild creates a **new task instance per execution**. Fields like `_fileTypes`, `_packageDefinitions`, `_lockFile`, etc. are instance fields — they don't cross-contaminate between threads because each thread gets its own instance. No locking needed.

### 4. Shared objects (LockFile, LockFileCache) are read-only after creation

`LockFileCache` uses MSBuild's `RegisteredTaskObject` system to share parsed lock files across task instances. The `LockFile` object is immutable after parsing, so sharing it is safe. Similarly, `NuGetPackageResolver` is read-only after construction.

### 5. The `ref _projectFileDependencies` pattern is safe

`IsTransitiveProjectReference` takes `ref HashSet<string> directProjectDependencies` and lazily initializes it. This looks dangerous, but it's safe because:
- It operates on instance fields (not static)
- Each task instance runs in a single thread
- The ref mutation happens within one task's `ExecuteCore()` call

## Stress Test Patterns

### Pattern 1: Concurrent execution with distinct project directories

The core test. N task instances run in parallel, each with its own `TaskEnvironment` pointing to a unique project directory. Use a `Barrier` to synchronize start for maximum contention.

```csharp
[Theory]
[InlineData(4)]
[InlineData(16)]
[InlineData(64)]
public void ConcurrentExecutionWithDistinctProjectDirs(int parallelism)
{
    var errors = new ConcurrentBag<string>();
    var barrier = new Barrier(parallelism);

    Parallel.For(0, parallelism, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, i =>
    {
        var projectDir = CreateUniqueProjectDir(i);
        var task = CreateTaskForProjectDir(projectDir, i);
        task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);

        barrier.SignalAndWait(); // maximize contention

        task.Execute();
        // Verify resolved paths match expected for THIS project dir
    });
}
```

**What it catches**: Cross-talk between threads via shared mutable state, static fields, or CWD dependency.

### Pattern 2: Same data, different project directories

Multiple tasks process identical lock file content but from different project directories. The same relative path (e.g., `../ClassLib/ClassLib.csproj`) must resolve to different absolute paths.

**Key test design detail**: Each project dir must have a **unique parent directory**. If all project dirs are siblings under `Temp/`, then `../ClassLib` resolves to the same path for all of them. Use nested dirs:

```
Temp/rpd-stress-<guid1>/proj0/myproject.csproj  →  ../ClassLib = Temp/rpd-stress-<guid1>/ClassLib
Temp/rpd-stress-<guid2>/proj1/myproject.csproj  →  ../ClassLib = Temp/rpd-stress-<guid2>/ClassLib
```

### Pattern 3: Mixed null/non-null TaskEnvironment

Half the tasks use `TaskEnvironment`, half don't. Verifies the null-check fallback path works correctly under concurrent access and doesn't interfere with the `TaskEnvironment` path.

### Pattern 4: Shared LockFile object

Multiple tasks share a single `LockFile` instance (simulating `LockFileCache` behavior in real builds). Verifies no shared mutable state leaks through the lock file.

### Pattern 5: Relative ProjectPath

Set `ProjectPath` to a relative path like `subdir/myproject.csproj`. This exercises `GetAbsolutePath` with a relative input from `Path.GetDirectoryName()`, proving the task resolves it via `TaskEnvironment.ProjectDirectory` rather than CWD.

### Pattern 6: Edge cases

- **Paths with spaces and special characters** (`My Lib`, `(1)`)
- **Multiple project references** in the same lock file with different relative paths
- **Repeated execution** with fresh instances to verify no state accumulation
- **CWD stability check**: verify `Directory.GetCurrentDirectory()` is unchanged after task execution

## Test Infrastructure

### Temp directory management

Use `ConcurrentBag<string>` (not `List<string>`) for tracking temp directories in concurrent tests. Clean up in `Dispose()`:

```csharp
private readonly ConcurrentBag<string> _tempDirs = new();

public void Dispose()
{
    foreach (var dir in _tempDirs)
    {
        try { Directory.Delete(dir, true); } catch { }
    }
}
```

### Creating test tasks with project references

To trigger `GetAbsolutePathFromProjectRelativePath`, the lock file must contain a library with `type: "project"` and a non-empty `msbuildProject` field:

```csharp
string classLibDefn = CreateProjectLibrary("ClassLib/1.0.0",
    path: "../ClassLib/project.json",
    msbuildProject: "../ClassLib/ClassLib.csproj");  // ← this triggers the forbidden API
string targetLib = CreateTargetLibrary("ClassLib/1.0.0", "project");
```

### Setting TaskEnvironment on migrated tasks

Since the test must compile against both migrated and unmigrated code, use reflection:

```csharp
var teProp = task.GetType().GetProperty("TaskEnvironment");
teProp.Should().NotBeNull("task must have a TaskEnvironment property");
teProp.SetValue(task, TaskEnvironmentHelper.CreateForTest(projectDir));
```

Or, if the task already implements `IMultiThreadableTask`, set it directly:

```csharp
task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir);
```

## Checklist for Stress Testing a Migrated Task

- [ ] Identify which method(s) use `TaskEnvironment` (the migrated forbidden API calls)
- [ ] Identify what input properties trigger those code paths (e.g., project-type libraries for `ResolvePackageDependencies`)
- [ ] Write concurrent execution test with `Parallel.For` + `Barrier`
- [ ] Write same-data-different-dirs test to prove path isolation
- [ ] Write mixed null/non-null `TaskEnvironment` test
- [ ] Write relative `ProjectPath` (or equivalent input) test
- [ ] Verify CWD is not modified during execution
- [ ] Run with high parallelism (64+ threads) to surface race conditions
- [ ] All stress tests should pass — then delete them (not for commit)
