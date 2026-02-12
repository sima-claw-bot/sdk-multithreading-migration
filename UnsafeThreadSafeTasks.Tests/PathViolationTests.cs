#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests;

public static class TestHelper
{
    /// <summary>
    /// Runs two task instances concurrently using a Barrier for synchronization.
    /// Each task gets a different project directory but the same relative InputPath.
    /// Returns the Result property of each task after execution.
    /// </summary>
    public static (string Result1, string Result2) RunTaskConcurrently(
        Type taskType,
        string projectDir1,
        string projectDir2,
        string inputPath)
    {
        var barrier = new Barrier(2);
        string? result1 = null;
        string? result2 = null;
        Exception? ex1 = null;
        Exception? ex2 = null;

        var thread1 = new Thread(() =>
        {
            try
            {
                var task = CreateAndConfigureTask(taskType, projectDir1, inputPath);
                barrier.SignalAndWait();
                task.Execute();
                result1 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
            }
            catch (Exception ex) { ex1 = ex; }
        });

        var thread2 = new Thread(() =>
        {
            try
            {
                var task = CreateAndConfigureTask(taskType, projectDir2, inputPath);
                barrier.SignalAndWait();
                task.Execute();
                result2 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
            }
            catch (Exception ex) { ex2 = ex; }
        });

        thread1.Start();
        thread2.Start();
        thread1.Join();
        thread2.Join();

        if (ex1 != null) throw new AggregateException("Task 1 failed", ex1);
        if (ex2 != null) throw new AggregateException("Task 2 failed", ex2);

        return (result1!, result2!);
    }

    private static Task CreateAndConfigureTask(Type taskType, string projectDir, string inputPath)
    {
        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = new TestBuildEngine();

        taskType.GetProperty("InputPath")!.SetValue(task, inputPath);

        if (task is IMultiThreadableTask multiThreadable)
        {
            multiThreadable.TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir };
        }

        return task;
    }
}

internal class TestBuildEngine : IBuildEngine
{
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        IDictionary globalProperties, IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) { }
    public void LogMessageEvent(BuildMessageEventArgs e) { }
    public void LogWarningEvent(BuildWarningEventArgs e) { }
}

/// <summary>
/// Black-box concurrent tests for all 7 PathViolation tasks.
/// Each test creates two distinct temp directories as project dirs,
/// places test files under those dirs, and runs two task instances
/// concurrently with a Barrier. Each task gets a different project dir
/// but the same relative InputPath.
/// </summary>
public class PathViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempProjectDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pvtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }

    #region RelativePathToDirectoryExists

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.RelativePathToDirectoryExists))]
    public void RelativePathToDirectoryExists_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "testsubdir";

        Directory.CreateDirectory(Path.Combine(dir1, relativePath));
        Directory.CreateDirectory(Path.Combine(dir2, relativePath));

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        Assert.Equal("True", result1);
        Assert.Equal("True", result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.RelativePathToDirectoryExists))]
    public void RelativePathToDirectoryExists_Unsafe_ResolvesAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "testsubdir";

        // Create subdirs only under the project dirs, not under CWD
        Directory.CreateDirectory(Path.Combine(dir1, relativePath));
        Directory.CreateDirectory(Path.Combine(dir2, relativePath));

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD, so both return the same result
        Assert.Equal(result1, result2);
    }

    #endregion

    #region RelativePathToFileExists

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.RelativePathToFileExists))]
    public void RelativePathToFileExists_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "testfile.txt";

        File.WriteAllText(Path.Combine(dir1, relativePath), "content1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "content2");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        Assert.Equal("True", result1);
        Assert.Equal("True", result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.RelativePathToFileExists))]
    public void RelativePathToFileExists_Unsafe_ResolvesAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "testfile.txt";

        File.WriteAllText(Path.Combine(dir1, relativePath), "content1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "content2");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD, so both return the same result
        Assert.Equal(result1, result2);
    }

    #endregion

    #region RelativePathToFileStream

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.RelativePathToFileStream))]
    public void RelativePathToFileStream_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "streamtest.txt";

        File.WriteAllText(Path.Combine(dir1, relativePath), "from_dir1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "from_dir2");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Each task reads from its own project dir
        Assert.Equal("from_dir1", result1);
        Assert.Equal("from_dir2", result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.RelativePathToFileStream))]
    public void RelativePathToFileStream_Unsafe_ResolvesAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "streamtest.txt";

        File.WriteAllText(Path.Combine(dir1, relativePath), "from_dir1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "from_dir2");

        // Unsafe: resolves against CWD which doesn't have the file -> throws
        Assert.ThrowsAny<Exception>(() =>
            TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath));
    }

    #endregion

    #region RelativePathToXDocument

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.RelativePathToXDocument))]
    public void RelativePathToXDocument_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "test.xml";

        File.WriteAllText(Path.Combine(dir1, relativePath), "<Project />");
        File.WriteAllText(Path.Combine(dir2, relativePath), "<Configuration />");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Each task reads from its own project dir
        Assert.Equal("Project", result1);
        Assert.Equal("Configuration", result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.RelativePathToXDocument))]
    public void RelativePathToXDocument_Unsafe_ResolvesAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "test.xml";

        File.WriteAllText(Path.Combine(dir1, relativePath), "<Project />");
        File.WriteAllText(Path.Combine(dir2, relativePath), "<Configuration />");

        // Unsafe: resolves against CWD which doesn't have the file -> throws
        Assert.ThrowsAny<Exception>(() =>
            TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath));
    }

    #endregion

    #region UsesPathGetFullPath_AttributeOnly

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.UsesPathGetFullPath_AttributeOnly))]
    public void UsesPathGetFullPath_AttributeOnly_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Fixed: each result resolves to its own project dir
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.UsesPathGetFullPath_AttributeOnly))]
    public void UsesPathGetFullPath_AttributeOnly_Unsafe_BothResolveAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD, so both produce the same result
        Assert.Equal(result1, result2);
        // Relative paths become absolute via CWD, not via project dir
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region UsesPathGetFullPath_ForCanonicalization

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.UsesPathGetFullPath_ForCanonicalization))]
    public void UsesPathGetFullPath_ForCanonicalization_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("a", "..", "b", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Fixed: each result resolves to its own project dir and is canonicalized
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", result1);
        Assert.DoesNotContain("..", result2);
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.UsesPathGetFullPath_ForCanonicalization))]
    public void UsesPathGetFullPath_ForCanonicalization_Unsafe_BothResolveAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("a", "..", "b", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD, so both produce the same result
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region UsesPathGetFullPath_IgnoresTaskEnv

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.UsesPathGetFullPath_IgnoresTaskEnv))]
    public void UsesPathGetFullPath_IgnoresTaskEnv_Fixed_ResolvesToOwnProjectDir(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Fixed: each result resolves to its own project dir
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(result1, result2);
    }

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.UsesPathGetFullPath_IgnoresTaskEnv))]
    public void UsesPathGetFullPath_IgnoresTaskEnv_Unsafe_BothResolveAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD despite TaskEnvironment being set
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
