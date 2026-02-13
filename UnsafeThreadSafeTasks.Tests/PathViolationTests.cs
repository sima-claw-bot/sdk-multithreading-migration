#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests;

public class PathViolationUnitTests : IDisposable
{
    private readonly string _tempDir;

    public PathViolationUnitTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PathViolationUnitTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region TaskZeta01

    [Fact]
    public void RelativePathToDirectoryExists_ExecuteReturnsTrue()
    {
        var task = new TaskZeta01 { InputPath = _tempDir, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToDirectoryExists_AbsolutePath_ReturnsCorrectResult()
    {
        var task = new TaskZeta01 { InputPath = _tempDir, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_NonExistentDir_ReturnsFalse()
    {
        var task = new TaskZeta01
        {
            InputPath = Path.Combine(_tempDir, "does_not_exist"),
            BuildEngine = new FakeBuildEngine()
        };
        task.Execute();
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_RelativePath_ResolvesAgainstCwd()
    {
        // Demonstrates the unsafe behavior: relative paths resolve against CWD, not a project dir
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var task = new TaskZeta01
        {
            InputPath = "subdir",
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta02

    [Fact]
    public void RelativePathToFileExists_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "hello");

        var task = new TaskZeta02 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToFileExists_ExistingFile_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "hello");

        var task = new TaskZeta02 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_NonExistentFile_ReturnsFalse()
    {
        var task = new TaskZeta02
        {
            InputPath = Path.Combine(_tempDir, "no_such_file.txt"),
            BuildEngine = new FakeBuildEngine()
        };
        task.Execute();
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_RelativePath_ResolvesAgainstCwd()
    {
        var filePath = Path.Combine(_tempDir, "relative_test.txt");
        File.WriteAllText(filePath, "content");

        var task = new TaskZeta02
        {
            InputPath = "relative_test.txt",
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta03

    [Fact]
    public void RelativePathToFileStream_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "stream_test.txt");
        File.WriteAllText(filePath, "first line\nsecond line");

        var task = new TaskZeta03 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToFileStream_ReadsFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "stream_test.txt");
        File.WriteAllText(filePath, "first line\nsecond line");

        var task = new TaskZeta03 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("first line", task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_RelativePath_ResolvesAgainstCwd()
    {
        var filePath = Path.Combine(_tempDir, "rel_stream.txt");
        File.WriteAllText(filePath, "relative content");

        var task = new TaskZeta03
        {
            InputPath = "rel_stream.txt",
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal("relative content", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta04

    [Fact]
    public void RelativePathToXDocument_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<Root><Child /></Root>");

        var task = new TaskZeta04 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToXDocument_ReturnsRootElementName()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<MyRoot><Child /></MyRoot>");

        var task = new TaskZeta04 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("MyRoot", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_RelativePath_ResolvesAgainstCwd()
    {
        var filePath = Path.Combine(_tempDir, "rel_doc.xml");
        File.WriteAllText(filePath, "<Project />");

        var task = new TaskZeta04
        {
            InputPath = "rel_doc.xml",
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal("Project", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta05

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ExecuteReturnsTrue()
    {
        var task = new TaskZeta05
        {
            InputPath = "somefile.txt",
            BuildEngine = new FakeBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_AbsoluteInput_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var task = new TaskZeta05
        {
            InputPath = absPath,
            BuildEngine = new FakeBuildEngine()
        };
        task.Execute();
        Assert.Equal(absPath, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_RelativePath_ResolvesAgainstCwd()
    {
        var task = new TaskZeta05
        {
            InputPath = "relative.txt",
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal(Path.Combine(_tempDir, "relative.txt"), task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta06

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ExecuteReturnsTrue()
    {
        var task = new TaskZeta06
        {
            InputPath = _tempDir,
            BuildEngine = new FakeBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ResolvesDotDotSegments()
    {
        var inputPath = Path.Combine(_tempDir, "sub", "..", "file.txt");
        var expectedPath = Path.Combine(_tempDir, "file.txt");

        var task = new TaskZeta06
        {
            InputPath = inputPath,
            BuildEngine = new FakeBuildEngine()
        };
        task.Execute();
        Assert.Equal(expectedPath, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_RelativePath_ResolvesAgainstCwd()
    {
        var task = new TaskZeta06
        {
            InputPath = Path.Combine("sub", "..", "file.txt"),
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();
            Assert.Equal(Path.Combine(_tempDir, "file.txt"), task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region TaskZeta07

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ImplementsIMultiThreadableTask()
    {
        var task = new TaskZeta07();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ExecuteReturnsTrue()
    {
        var task = new TaskZeta07
        {
            InputPath = "somefile.txt",
            BuildEngine = new FakeBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_IgnoresProjectDirectory()
    {
        // Demonstrates the bug: even though TaskEnvironment has a ProjectDirectory,
        // the task uses Path.GetFullPath which resolves against CWD instead.
        var projectDir = Path.Combine(_tempDir, "project");
        Directory.CreateDirectory(projectDir);

        var task = new TaskZeta07
        {
            InputPath = "file.txt",
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
            BuildEngine = new FakeBuildEngine()
        };

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempDir);
            task.Execute();

            // The task should ideally resolve against projectDir, but it doesn't
            var expectedFromTaskEnv = Path.Combine(projectDir, "file.txt");
            var expectedFromCwd = Path.Combine(_tempDir, "file.txt");

            Assert.Equal(expectedFromCwd, task.Result);
            Assert.NotEqual(expectedFromTaskEnv, task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_HasTaskEnvironmentProperty()
    {
        var taskEnv = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new TaskZeta07 { TaskEnvironment = taskEnv };
        Assert.Same(taskEnv, task.TaskEnvironment);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for testing MSBuild tasks.
/// </summary>
internal class FakeBuildEngine : IBuildEngine
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

    #region TaskZeta01

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta01))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta01))]
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

    #region TaskZeta02

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta02))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta02))]
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

    #region TaskZeta03

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta03))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta03))]
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

    #region TaskZeta04

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta04))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta04))]
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

    #region TaskZeta05

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta05))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta05))]
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

    #region TaskZeta06

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta06))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta06))]
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

    #region TaskZeta07

    [Theory]
    [Trait("Category", "PathViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedThreadSafeTasks.PathViolations.TaskZeta07))]
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
    [InlineData(typeof(UnsafeThreadSafeTasks.PathViolations.TaskZeta07))]
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
