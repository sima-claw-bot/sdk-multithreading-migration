using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

public class PathViolationTaskTests : IDisposable
{
    private readonly string _tempDir;

    public PathViolationTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"PathViolationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region TaskZeta01

    [Fact]
    public void RelativePathToDirectoryExists_WithAbsolutePath_ReturnsTrue()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var task = new TaskZeta01 { InputPath = subDir, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithNonExistentPath_ReturnsFalse()
    {
        var task = new TaskZeta01
        {
            InputPath = Path.Combine(_tempDir, "nonexistent"),
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithRelativePath_ResolvesAgainstProcessCwd()
    {
        // Demonstrates the bug: relative paths resolve against process CWD
        var task = new TaskZeta01
        {
            InputPath = "some_relative_dir",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        // The relative path resolves against the process CWD, not a project directory
        Assert.Equal(Directory.Exists("some_relative_dir").ToString(), task.Result);
    }

    #endregion

    #region TaskZeta02

    [Fact]
    public void RelativePathToFileExists_WithAbsolutePath_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "testfile.txt");
        File.WriteAllText(filePath, "content");

        var task = new TaskZeta02 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_WithNonExistentPath_ReturnsFalse()
    {
        var task = new TaskZeta02
        {
            InputPath = Path.Combine(_tempDir, "nonexistent.txt"),
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_WithRelativePath_ResolvesAgainstProcessCwd()
    {
        var task = new TaskZeta02
        {
            InputPath = "some_relative_file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(File.Exists("some_relative_file.txt").ToString(), task.Result);
    }

    #endregion

    #region TaskZeta03

    [Fact]
    public void RelativePathToFileStream_WithAbsolutePath_ReadsFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "streamtest.txt");
        File.WriteAllText(filePath, "hello world\nsecond line");

        var task = new TaskZeta03 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello world", task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_WithEmptyFile_ReturnsEmptyString()
    {
        var filePath = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(filePath, "");

        var task = new TaskZeta03 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_WithNonExistentFile_Throws()
    {
        var task = new TaskZeta03
        {
            InputPath = Path.Combine(_tempDir, "nonexistent.txt"),
            BuildEngine = new FakeBuildEngine()
        };

        Assert.Throws<FileNotFoundException>(() => task.Execute());
    }

    #endregion

    #region TaskZeta04

    [Fact]
    public void RelativePathToXDocument_WithAbsolutePath_ReturnsRootElementName()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<Project><PropertyGroup /></Project>");

        var task = new TaskZeta04 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Project", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_WithNonExistentFile_Throws()
    {
        var task = new TaskZeta04
        {
            InputPath = Path.Combine(_tempDir, "nonexistent.xml"),
            BuildEngine = new FakeBuildEngine()
        };

        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    [Fact]
    public void RelativePathToXDocument_WithDifferentRoot_ReturnsCorrectName()
    {
        var filePath = Path.Combine(_tempDir, "config.xml");
        File.WriteAllText(filePath, "<Configuration><Setting /></Configuration>");

        var task = new TaskZeta04 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Configuration", task.Result);
    }

    #endregion

    #region TaskZeta05

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithAbsolutePath_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");

        var task = new TaskZeta05 { InputPath = absPath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Path.GetFullPath(absPath), task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithRelativePath_ResolvesAgainstProcessCwd()
    {
        var task = new TaskZeta05
        {
            InputPath = "relative/path.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        // Demonstrates the bug: resolves against process CWD
        var expected = Path.GetFullPath("relative/path.txt");
        Assert.Equal(expected, task.Result);
    }

    #endregion

    #region TaskZeta06

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_WithDotDotSegments_CanonicalizesPath()
    {
        var inputPath = Path.Combine(_tempDir, "a", "..", "b", "file.txt");

        var task = new TaskZeta06
        {
            InputPath = inputPath,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Path.GetFullPath(inputPath), task.Result);
        Assert.DoesNotContain("..", task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_WithRelativePath_ResolvesAgainstProcessCwd()
    {
        var task = new TaskZeta06
        {
            InputPath = "subdir/../file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        var expected = Path.GetFullPath("subdir/../file.txt");
        Assert.Equal(expected, task.Result);
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
    public void UsesPathGetFullPath_IgnoresTaskEnv_HasTaskEnvironmentProperty()
    {
        var env = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new TaskZeta07 { TaskEnvironment = env };

        Assert.Same(env, task.TaskEnvironment);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_WithRelativePath_IgnoresProjectDirectory()
    {
        var env = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new TaskZeta07
        {
            TaskEnvironment = env,
            InputPath = "relative/file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        // Bug: uses Path.GetFullPath (process CWD) instead of TaskEnvironment.GetAbsolutePath
        var cwdResolved = Path.GetFullPath("relative/file.txt");
        var projectResolved = Path.GetFullPath(Path.Combine(_tempDir, "relative/file.txt"));
        Assert.Equal(cwdResolved, task.Result);
        // If the CWD != project dir, the result differs from what it should be
        if (Directory.GetCurrentDirectory() != _tempDir)
        {
            Assert.NotEqual(projectResolved, task.Result);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_WithAbsolutePath_ReturnsCorrectResult()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var env = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new TaskZeta07
        {
            TaskEnvironment = env,
            InputPath = absPath,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Path.GetFullPath(absPath), task.Result);
    }

    #endregion

    #region Structural Validation

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllPathViolationTasks_InheritFromMSBuildTask(Type taskType)
    {
        Assert.True(typeof(Task).IsAssignableFrom(taskType));
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllPathViolationTasks_InputPathHasRequiredAttribute(Type taskType)
    {
        var prop = taskType.GetProperty("InputPath");
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<RequiredAttribute>());
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllPathViolationTasks_ResultHasOutputAttribute(Type taskType)
    {
        var prop = taskType.GetProperty("Result");
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetCustomAttribute<OutputAttribute>());
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllPathViolationTasks_DefaultPropertyValues(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType)!;
        var inputPath = (string)taskType.GetProperty("InputPath")!.GetValue(instance)!;
        var result = (string)taskType.GetProperty("Result")!.GetValue(instance)!;
        Assert.Equal(string.Empty, inputPath);
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    public void NonIMultiThreadableTasks_DoNotImplementInterface(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_IsOnlyIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(TaskZeta07)));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void RelativePathToFileStream_WithMultipleLines_ReturnsOnlyFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "multiline.txt");
        File.WriteAllText(filePath, "first\nsecond\nthird");

        var task = new TaskZeta03 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("first", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_WithAttributes_ReturnsRootNameOnly()
    {
        var filePath = Path.Combine(_tempDir, "attrs.xml");
        File.WriteAllText(filePath, "<Root xmlns=\"http://example.com\" version=\"1.0\"><Child /></Root>");

        var task = new TaskZeta04 { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Root", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithAbsoluteNonExistentPath_ReturnsFalseString()
    {
        var task = new TaskZeta01
        {
            InputPath = Path.Combine(_tempDir, "does_not_exist_dir"),
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithDotDotSegments_CanonicalizesPath()
    {
        var inputPath = Path.Combine(_tempDir, "a", "..", "b", "file.txt");

        var task = new TaskZeta05
        {
            InputPath = inputPath,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.DoesNotContain("..", task.Result);
        Assert.EndsWith(Path.Combine("b", "file.txt"), task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_DefaultTaskEnvironment()
    {
        var task = new TaskZeta07();
        Assert.NotNull(task.TaskEnvironment);
        Assert.Equal(string.Empty, task.TaskEnvironment.ProjectDirectory);
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void NonThrowingTasks_Execute_AlwaysReturnsTrue(Type taskType)
    {
        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = new FakeBuildEngine();
        taskType.GetProperty("InputPath")!.SetValue(task, _tempDir);

        if (task is IMultiThreadableTask mt)
            mt.TaskEnvironment = new TaskEnvironment { ProjectDirectory = _tempDir };

        Assert.True(task.Execute());
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
