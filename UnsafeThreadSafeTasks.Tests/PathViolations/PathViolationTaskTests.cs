using System;
using System.Collections;
using System.IO;
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

    #region RelativePathToDirectoryExists

    [Fact]
    public void RelativePathToDirectoryExists_WithAbsolutePath_ReturnsTrue()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var task = new RelativePathToDirectoryExists { InputPath = subDir, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithNonExistentPath_ReturnsFalse()
    {
        var task = new RelativePathToDirectoryExists
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
        var task = new RelativePathToDirectoryExists
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

    #region RelativePathToFileExists

    [Fact]
    public void RelativePathToFileExists_WithAbsolutePath_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "testfile.txt");
        File.WriteAllText(filePath, "content");

        var task = new RelativePathToFileExists { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_WithNonExistentPath_ReturnsFalse()
    {
        var task = new RelativePathToFileExists
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
        var task = new RelativePathToFileExists
        {
            InputPath = "some_relative_file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(File.Exists("some_relative_file.txt").ToString(), task.Result);
    }

    #endregion

    #region RelativePathToFileStream

    [Fact]
    public void RelativePathToFileStream_WithAbsolutePath_ReadsFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "streamtest.txt");
        File.WriteAllText(filePath, "hello world\nsecond line");

        var task = new RelativePathToFileStream { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello world", task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_WithEmptyFile_ReturnsEmptyString()
    {
        var filePath = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(filePath, "");

        var task = new RelativePathToFileStream { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_WithNonExistentFile_Throws()
    {
        var task = new RelativePathToFileStream
        {
            InputPath = Path.Combine(_tempDir, "nonexistent.txt"),
            BuildEngine = new FakeBuildEngine()
        };

        Assert.Throws<FileNotFoundException>(() => task.Execute());
    }

    #endregion

    #region RelativePathToXDocument

    [Fact]
    public void RelativePathToXDocument_WithAbsolutePath_ReturnsRootElementName()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<Project><PropertyGroup /></Project>");

        var task = new RelativePathToXDocument { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Project", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_WithNonExistentFile_Throws()
    {
        var task = new RelativePathToXDocument
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

        var task = new RelativePathToXDocument { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Configuration", task.Result);
    }

    #endregion

    #region UsesPathGetFullPath_AttributeOnly

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithAbsolutePath_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");

        var task = new UsesPathGetFullPath_AttributeOnly { InputPath = absPath, BuildEngine = new FakeBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Path.GetFullPath(absPath), task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithRelativePath_ResolvesAgainstProcessCwd()
    {
        var task = new UsesPathGetFullPath_AttributeOnly
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

    #region UsesPathGetFullPath_ForCanonicalization

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_WithDotDotSegments_CanonicalizesPath()
    {
        var inputPath = Path.Combine(_tempDir, "a", "..", "b", "file.txt");

        var task = new UsesPathGetFullPath_ForCanonicalization
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
        var task = new UsesPathGetFullPath_ForCanonicalization
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

    #region UsesPathGetFullPath_IgnoresTaskEnv

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ImplementsIMultiThreadableTask()
    {
        var task = new UsesPathGetFullPath_IgnoresTaskEnv();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_HasTaskEnvironmentProperty()
    {
        var env = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new UsesPathGetFullPath_IgnoresTaskEnv { TaskEnvironment = env };

        Assert.Same(env, task.TaskEnvironment);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_WithRelativePath_IgnoresProjectDirectory()
    {
        var env = new TaskEnvironment { ProjectDirectory = _tempDir };
        var task = new UsesPathGetFullPath_IgnoresTaskEnv
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
        var task = new UsesPathGetFullPath_IgnoresTaskEnv
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
