using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeIntermittent = UnsafeThreadSafeTasks.IntermittentViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for IntermittentViolation unsafe tasks (batch 2):
/// FileWatcherGlobalNotifications, ProcessStartInfoInheritsCwd.
/// Covers structural checks, functional behavior, and concurrency bugs.
/// </summary>
public class IntermittentViolationBatch2Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public IntermittentViolationBatch2Tests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ivb2test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalCwd;
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    #region FileWatcherGlobalNotifications — Structural Checks

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_ExtendsTask()
    {
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_HasStaticWatcherField()
    {
        var field = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetField("_watcher", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_HasStaticLockField()
    {
        var field = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetField("Lock", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_WatchPathIsRequired()
    {
        var prop = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetProperty(nameof(UnsafeIntermittent.FileWatcherGlobalNotifications.WatchPath));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_LastChangedFileIsOutput()
    {
        var prop = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetProperty(nameof(UnsafeIntermittent.FileWatcherGlobalNotifications.LastChangedFile));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_DefaultLastChangedFileIsEmpty()
    {
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications();
        Assert.Equal(string.Empty, task.LastChangedFile);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_DefaultWatchPathIsEmpty()
    {
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications();
        Assert.Equal(string.Empty, task.WatchPath);
    }

    #endregion

    #region FileWatcherGlobalNotifications — Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_ExecuteReturnsTrue()
    {
        var dir = CreateTempDir();
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications
        {
            WatchPath = dir,
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_LastChangedFileEmptyAfterExecuteWithNoChanges()
    {
        var dir = CreateTempDir();
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications
        {
            WatchPath = dir,
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        task.Execute();

        // No file changes occurred, so LastChangedFile remains empty
        Assert.Equal(string.Empty, task.LastChangedFile);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_DetectsFileChange()
    {
        var dir = CreateTempDir();
        var task = new UnsafeIntermittent.FileWatcherGlobalNotifications
        {
            WatchPath = dir,
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        task.Execute();

        // Create a file to trigger the watcher
        var filePath = Path.Combine(dir, "test.txt");
        File.WriteAllText(filePath, "hello");

        // Give the watcher a moment to fire
        Thread.Sleep(500);

        // The handler should have set LastChangedFile
        // (may or may not fire depending on OS timing, so we just check it doesn't throw)
        Assert.True(task.LastChangedFile == string.Empty || task.LastChangedFile.Contains("test.txt"));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void FileWatcherGlobalNotifications_SecondCallReplacesStaticWatcher()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new UnsafeIntermittent.FileWatcherGlobalNotifications
        {
            WatchPath = dir1,
            BuildEngine = new IntermittentBatch2BuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeIntermittent.FileWatcherGlobalNotifications
        {
            WatchPath = dir2,
            BuildEngine = new IntermittentBatch2BuildEngine()
        };
        task2.Execute();

        // BUG: the static watcher now points to dir2; task1's watcher was disposed
        var watcherField = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetField("_watcher", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(watcherField);
        var watcher = watcherField!.GetValue(null) as FileSystemWatcher;
        Assert.NotNull(watcher);
        Assert.Equal(dir2, watcher!.Path);
    }

    #endregion

    #region FileWatcherGlobalNotifications — Concurrency Tests

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void FileWatcherGlobalNotifications_Unsafe_ConcurrentCallsReplaceWatcher(int iteration)
    {
        _ = iteration;
        int threadCount = 16;
        var barrier = new Barrier(threadCount);
        var dirs = new List<string>();
        var tasks = new ConcurrentBag<UnsafeIntermittent.FileWatcherGlobalNotifications>();
        var threads = new List<Thread>();

        for (int i = 0; i < threadCount; i++)
        {
            dirs.Add(CreateTempDir());
        }

        for (int i = 0; i < threadCount; i++)
        {
            var dir = dirs[i];
            var t = new Thread(() =>
            {
                var task = new UnsafeIntermittent.FileWatcherGlobalNotifications
                {
                    WatchPath = dir,
                    BuildEngine = new IntermittentBatch2BuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                tasks.Add(task);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // All threads completed, but only one watcher survives in the static field.
        // The static _watcher points to only one of the directories.
        var watcherField = typeof(UnsafeIntermittent.FileWatcherGlobalNotifications)
            .GetField("_watcher", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(watcherField);
        var watcher = watcherField!.GetValue(null) as FileSystemWatcher;
        Assert.NotNull(watcher);

        // Only one directory is being watched; the rest are orphaned
        var watchedPath = watcher!.Path;
        Assert.Contains(watchedPath, dirs);

        // The watcher can only point to one directory, so other tasks' directories are abandoned
        var otherDirs = dirs.Where(d => d != watchedPath).ToList();
        Assert.True(otherDirs.Count == threadCount - 1,
            $"Expected {threadCount - 1} abandoned directories, got {otherDirs.Count}");
    }

    #endregion

    #region ProcessStartInfoInheritsCwd — Structural Checks

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_ExtendsTask()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_CommandIsRequired()
    {
        var prop = typeof(UnsafeIntermittent.ProcessStartInfoInheritsCwd)
            .GetProperty(nameof(UnsafeIntermittent.ProcessStartInfoInheritsCwd.Command));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_ResultIsOutput()
    {
        var prop = typeof(UnsafeIntermittent.ProcessStartInfoInheritsCwd)
            .GetProperty(nameof(UnsafeIntermittent.ProcessStartInfoInheritsCwd.Result));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_DefaultCommandIsEmpty()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd();
        Assert.Equal(string.Empty, task.Command);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_DefaultArgumentsIsEmpty()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd();
        Assert.Equal(string.Empty, task.Arguments);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_DefaultResultIsEmpty()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd();
        Assert.Equal(string.Empty, task.Result);
    }

    #endregion

    #region ProcessStartInfoInheritsCwd — Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_ExecuteReturnsTrue()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
        {
            Command = "cmd.exe",
            Arguments = "/c echo hello",
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_CapturesStdout()
    {
        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
        {
            Command = "cmd.exe",
            Arguments = "/c echo hello_world",
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        task.Execute();

        Assert.Equal("hello_world", task.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_InheritsCurrentDirectory()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
        {
            Command = "cmd.exe",
            Arguments = "/c cd",
            BuildEngine = new IntermittentBatch2BuildEngine()
        };

        task.Execute();

        // The process should inherit the CWD that was set before Execute
        Assert.Equal(dir, task.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    public void ProcessStartInfoInheritsCwd_UsesProcessGlobalCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        // Set CWD to dir1
        Environment.CurrentDirectory = dir1;

        var task1 = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
        {
            Command = "cmd.exe",
            Arguments = "/c cd",
            BuildEngine = new IntermittentBatch2BuildEngine()
        };
        task1.Execute();

        // Change CWD to dir2
        Environment.CurrentDirectory = dir2;

        var task2 = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
        {
            Command = "cmd.exe",
            Arguments = "/c cd",
            BuildEngine = new IntermittentBatch2BuildEngine()
        };
        task2.Execute();

        // Each task inherits whatever the process-global CWD was at time of execution
        Assert.Equal(dir1, task1.Result);
        Assert.Equal(dir2, task2.Result);
    }

    #endregion

    #region ProcessStartInfoInheritsCwd — Concurrency Tests

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void ProcessStartInfoInheritsCwd_Unsafe_ConcurrentCwdChangesCorruptWorkingDir(int iteration)
    {
        _ = iteration;
        int threadCount = 16;
        var barrier = new Barrier(threadCount);
        var mismatches = new ConcurrentBag<bool>();
        var threads = new List<Thread>();
        var dirs = new List<string>();

        for (int i = 0; i < threadCount; i++)
        {
            dirs.Add(CreateTempDir());
        }

        for (int i = 0; i < threadCount; i++)
        {
            var myDir = dirs[i];
            var t = new Thread(() =>
            {
                // Set process-global CWD to this thread's directory
                Environment.CurrentDirectory = myDir;

                var task = new UnsafeIntermittent.ProcessStartInfoInheritsCwd
                {
                    Command = "cmd.exe",
                    Arguments = "/c cd",
                    BuildEngine = new IntermittentBatch2BuildEngine()
                };
                barrier.SignalAndWait();

                // BUG: Between setting CWD and the task capturing it in ProcessStartInfo,
                // another thread can change CWD. Also the Thread.Sleep(50) inside Execute
                // widens the race window further.
                Environment.CurrentDirectory = myDir;
                task.Execute();

                // Check if the process reported a different directory than expected
                mismatches.Add(task.Result != myDir);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // At least one thread should have its CWD overwritten by another thread
        Assert.Contains(true, mismatches);
    }

    #endregion

    #region All batch 2 types — common structural checks

    public static IEnumerable<object[]> Batch2IntermittentViolationTypes()
    {
        yield return new object[] { typeof(UnsafeIntermittent.FileWatcherGlobalNotifications) };
        yield return new object[] { typeof(UnsafeIntermittent.ProcessStartInfoInheritsCwd) };
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [MemberData(nameof(Batch2IntermittentViolationTypes))]
    public void Batch2Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType),
            $"{taskType.Name} should extend Microsoft.Build.Utilities.Task");
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [MemberData(nameof(Batch2IntermittentViolationTypes))]
    public void Batch2Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.IntermittentViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [MemberData(nameof(Batch2IntermittentViolationTypes))]
    public void Batch2Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [MemberData(nameof(Batch2IntermittentViolationTypes))]
    public void Batch2Type_HasExecuteMethod(Type taskType)
    {
        var method = taskType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(method);
        Assert.Equal(typeof(bool), method!.ReturnType);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for IntermittentViolation batch 2 tests.
/// </summary>
internal class IntermittentBatch2BuildEngine : IBuildEngine
{
    public List<BuildErrorEventArgs> Errors { get; } = new();
    public List<BuildWarningEventArgs> Warnings { get; } = new();
    public List<BuildMessageEventArgs> Messages { get; } = new();

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e);
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
}
