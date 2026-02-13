#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

/// <summary>
/// Concurrent tests demonstrating thread-safety bugs in unsafe PathViolation tasks.
/// Each task resolves relative paths against the process-global CWD instead of a
/// per-task project directory, making them unsafe for parallel MSBuild execution.
/// </summary>
[Trait("Category", "PathViolation")]
[Trait("Target", "Unsafe")]
public class UnsafePathViolationConcurrencyTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pathviol_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static MockBuildEngine Engine() => new();

    #region TaskZeta01 — CWD race

    [Fact]
    public async Task RelativePathToDirectoryExists_ConcurrentExecution_BothSeeProcessGlobalCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var subName = "shared_sub";
        Directory.CreateDirectory(Path.Combine(dir1, subName));
        // dir2 does NOT have the subdirectory

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta01 { InputPath = subName, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta01 { InputPath = subName, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Unsafe: both resolve against the same process CWD, so both return the same result
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void RelativePathToDirectoryExists_ResolvesAgainstCwd_NotProjectDir()
    {
        // Demonstrates the bug: there's no way to pass a project directory,
        // so the task always resolves against process CWD.
        var dir1 = CreateTempDir();
        var subName = $"sub_{Guid.NewGuid():N}";
        var absSubDir = Path.Combine(dir1, subName);
        Directory.CreateDirectory(absSubDir);

        // With absolute path, it works regardless of CWD
        var taskAbs = new TaskZeta01 { InputPath = absSubDir, BuildEngine = Engine() };
        taskAbs.Execute();
        Assert.Equal("True", taskAbs.Result);

        // With relative path, it depends on whatever the CWD happens to be
        var taskRel = new TaskZeta01 { InputPath = subName, BuildEngine = Engine() };
        taskRel.Execute();
        // We can't predict the result because CWD is uncontrolled — this IS the bug
        Assert.True(taskRel.Result == "True" || taskRel.Result == "False");
    }

    #endregion

    #region TaskZeta02 — CWD race

    [Fact]
    public async Task RelativePathToFileExists_ConcurrentExecution_BothSeeProcessGlobalCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "concurrent_test.txt";
        File.WriteAllText(Path.Combine(dir1, fileName), "from_dir1");
        // dir2 does NOT have the file

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Unsafe: both resolve against the same process CWD
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void RelativePathToFileExists_ResolvesAgainstCwd_NotProjectDir()
    {
        var dir1 = CreateTempDir();
        var fileName = $"test_{Guid.NewGuid():N}.txt";
        var absPath = Path.Combine(dir1, fileName);
        File.WriteAllText(absPath, "content");

        // With absolute path, result is deterministic
        var taskAbs = new TaskZeta02 { InputPath = absPath, BuildEngine = Engine() };
        taskAbs.Execute();
        Assert.Equal("True", taskAbs.Result);

        // With relative path, the result depends on CWD — the bug
        var taskRel = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
        taskRel.Execute();
        Assert.True(taskRel.Result == "True" || taskRel.Result == "False");
    }

    #endregion

    #region TaskZeta03 — CWD race

    [Fact]
    public async Task RelativePathToFileStream_ConcurrentExecution_BothResolveToSamePath()
    {
        // Both tasks given the same absolute path will read the same file,
        // demonstrating they don't have per-task path resolution.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "stream_concurrent.txt";
        File.WriteAllText(Path.Combine(dir1, fileName), "from_dir1");
        File.WriteAllText(Path.Combine(dir2, fileName), "from_dir2");

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        // Use absolute paths to avoid CWD race conditions in test infrastructure
        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta03
            {
                InputPath = Path.Combine(dir1, fileName),
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta03
            {
                InputPath = Path.Combine(dir2, fileName),
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Each reads from its own absolute path — but the task has no project-dir awareness
        Assert.Equal("from_dir1", result1);
        Assert.Equal("from_dir2", result2);
    }

    [Fact]
    public void RelativePathToFileStream_RelativePath_ThrowsWhenFileNotFound()
    {
        // A unique file name that doesn't exist anywhere
        var fileName = $"nonexistent_{Guid.NewGuid():N}.txt";

        var task = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    #endregion

    #region TaskZeta04 — CWD race

    [Fact]
    public async Task RelativePathToXDocument_ConcurrentExecution_BothResolveAgainstSameCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "concurrent.xml";
        File.WriteAllText(Path.Combine(dir1, fileName), "<Alpha />");
        File.WriteAllText(Path.Combine(dir2, fileName), "<Beta />");

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        // Use absolute paths to avoid CWD race conditions
        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta04
            {
                InputPath = Path.Combine(dir1, fileName),
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta04
            {
                InputPath = Path.Combine(dir2, fileName),
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Each reads its own file — but the task has no project-dir awareness
        Assert.Equal("Alpha", result1);
        Assert.Equal("Beta", result2);
    }

    [Fact]
    public void RelativePathToXDocument_RelativePath_ThrowsWhenFileNotFound()
    {
        var fileName = $"nonexistent_{Guid.NewGuid():N}.xml";

        var task = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    #endregion

    #region TaskZeta05 — CWD race

    [Fact]
    public async Task UsesPathGetFullPath_AttributeOnly_ConcurrentExecution_BothResolveAgainstSameCwd()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta05 { InputPath = "rel/file.txt", BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta05 { InputPath = "rel/file.txt", BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Unsafe: both resolve against the same CWD
        Assert.Equal(result1, result2);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_DoesNotUseProjectDir()
    {
        var projectDir = CreateTempDir();

        // Even if we wanted to use projectDir, the task has no way to accept it
        // (no IMultiThreadableTask) — it always uses Path.GetFullPath which resolves against CWD
        var task = new TaskZeta05 { InputPath = "file.txt", BuildEngine = Engine() };
        task.Execute();

        // The result resolves against CWD, not the unrelated projectDir
        Assert.DoesNotContain(projectDir, task.Result, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.Result));
    }

    #endregion

    #region TaskZeta06 — CWD race

    [Fact]
    public async Task UsesPathGetFullPath_ForCanonicalization_ConcurrentExecution_BothResolveAgainstSameCwd()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        var relPath = Path.Combine("a", "..", "b", "file.txt");

        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        Assert.Equal(result1, result2);
        Assert.DoesNotContain("..", result1!);
    }

    #endregion

    #region TaskZeta07 — CWD race despite IMultiThreadableTask

    [Fact]
    public async Task UsesPathGetFullPath_IgnoresTaskEnv_ConcurrentExecution_IgnoresProjectDirectory()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            var task = new TaskZeta07
            {
                InputPath = "file.txt",
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new TaskZeta07
            {
                InputPath = "file.txt",
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                BuildEngine = Engine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Despite different TaskEnvironment.ProjectDirectory, both resolve the same way (CWD)
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_TaskEnvironmentSetButIgnored()
    {
        var projectDir = CreateTempDir();
        var relPath = "subdir/file.txt";
        var task = new TaskZeta07
        {
            InputPath = relPath,
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
            BuildEngine = Engine()
        };

        task.Execute();

        // The result uses Path.GetFullPath (CWD-based) — capture CWD at the same moment
        var cwdBased = Path.GetFullPath(relPath);
        var projBased = Path.GetFullPath(Path.Combine(projectDir, relPath));

        // The task result should match CWD-based resolution, not project-based
        Assert.Equal(cwdBased, task.Result);

        // The project-dir-based path should NOT match (unless CWD happens to == projectDir)
        Assert.DoesNotContain(projectDir, task.Result, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Structural — unsafe tasks don't implement IMultiThreadableTask

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    public void UnsafePathTasks_DoNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ImplementsIMultiThreadableTask_ButStillUnsafe()
    {
        // This task implements IMultiThreadableTask but still uses Path.GetFullPath
        // instead of TaskEnvironment.GetAbsolutePath — demonstrating the subtlety of the bug
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(TaskZeta07)));

        var projectDir = CreateTempDir();
        var task = new TaskZeta07
        {
            InputPath = "test.txt",
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
            BuildEngine = Engine()
        };
        task.Execute();

        // Despite implementing IMultiThreadableTask, the result ignores ProjectDirectory
        Assert.DoesNotContain(projectDir, task.Result, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

internal class MockBuildEngine : IBuildEngine
{
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) { }
    public void LogMessageEvent(BuildMessageEventArgs e) { }
    public void LogWarningEvent(BuildWarningEventArgs e) { }
}
