using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Fixed = FixedThreadSafeTasks.PathViolations;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

[Trait("Target", "Fixed")]
[Trait("Category", "PathViolation")]
public class FixedPathViolationTaskTests : IDisposable
{
    private readonly string _tempDir;

    public FixedPathViolationTaskTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"FixedPathViolationTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private TaskEnvironment CreateEnv() => new() { ProjectDirectory = _tempDir };

    #region RelativePathToDirectoryExists

    [Fact]
    public void RelativePathToDirectoryExists_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.RelativePathToDirectoryExists();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithRelativePath_ResolvesAgainstProjectDir()
    {
        var subDir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subDir);

        var env = CreateEnv();
        var task = new Fixed.RelativePathToDirectoryExists
        {
            TaskEnvironment = env,
            InputPath = "subdir",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithNonExistentRelativePath_ReturnsFalse()
    {
        var env = CreateEnv();
        var task = new Fixed.RelativePathToDirectoryExists
        {
            TaskEnvironment = env,
            InputPath = "nonexistent",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_WithAbsolutePath_ReturnsTrue()
    {
        var subDir = Path.Combine(_tempDir, "absdir");
        Directory.CreateDirectory(subDir);

        var env = CreateEnv();
        var task = new Fixed.RelativePathToDirectoryExists
        {
            TaskEnvironment = env,
            InputPath = subDir,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    #endregion

    #region RelativePathToFileExists

    [Fact]
    public void RelativePathToFileExists_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.RelativePathToFileExists();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void RelativePathToFileExists_WithRelativePath_ResolvesAgainstProjectDir()
    {
        File.WriteAllText(Path.Combine(_tempDir, "testfile.txt"), "content");

        var env = CreateEnv();
        var task = new Fixed.RelativePathToFileExists
        {
            TaskEnvironment = env,
            InputPath = "testfile.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_WithNonExistentRelativePath_ReturnsFalse()
    {
        var env = CreateEnv();
        var task = new Fixed.RelativePathToFileExists
        {
            TaskEnvironment = env,
            InputPath = "nonexistent.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("False", task.Result);
    }

    #endregion

    #region RelativePathToFileStream

    [Fact]
    public void RelativePathToFileStream_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.RelativePathToFileStream();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void RelativePathToFileStream_WithRelativePath_ResolvesAgainstProjectDir()
    {
        File.WriteAllText(Path.Combine(_tempDir, "stream.txt"), "hello world\nsecond line");

        var env = CreateEnv();
        var task = new Fixed.RelativePathToFileStream
        {
            TaskEnvironment = env,
            InputPath = "stream.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello world", task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_WithNonExistentFile_Throws()
    {
        var env = CreateEnv();
        var task = new Fixed.RelativePathToFileStream
        {
            TaskEnvironment = env,
            InputPath = "nonexistent.txt",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.Throws<FileNotFoundException>(() => task.Execute());
    }

    [Fact]
    public void RelativePathToFileStream_WithAbsolutePath_ReadsFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "absstream.txt");
        File.WriteAllText(filePath, "hello absolute\nsecond line");

        var env = CreateEnv();
        var task = new Fixed.RelativePathToFileStream
        {
            TaskEnvironment = env,
            InputPath = filePath,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello absolute", task.Result);
    }

    #endregion

    #region RelativePathToXDocument

    [Fact]
    public void RelativePathToXDocument_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.RelativePathToXDocument();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void RelativePathToXDocument_WithRelativePath_ResolvesAgainstProjectDir()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.xml"), "<Project><PropertyGroup /></Project>");

        var env = CreateEnv();
        var task = new Fixed.RelativePathToXDocument
        {
            TaskEnvironment = env,
            InputPath = "test.xml",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("Project", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_WithNonExistentFile_Throws()
    {
        var env = CreateEnv();
        var task = new Fixed.RelativePathToXDocument
        {
            TaskEnvironment = env,
            InputPath = "nonexistent.xml",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    #endregion

    #region UsesPathGetFullPath_AttributeOnly

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.UsesPathGetFullPath_AttributeOnly();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithRelativePath_ResolvesAgainstProjectDir()
    {
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_AttributeOnly
        {
            TaskEnvironment = env,
            InputPath = "relative/path.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        var expected = Path.GetFullPath(Path.Combine(_tempDir, "relative/path.txt"));
        Assert.Equal(expected, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_WithAbsolutePath_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_AttributeOnly
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

    #region UsesPathGetFullPath_ForCanonicalization

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.UsesPathGetFullPath_ForCanonicalization();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_WithRelativePath_ResolvesAgainstProjectDir()
    {
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_ForCanonicalization
        {
            TaskEnvironment = env,
            InputPath = "subdir/../file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        var expected = Path.GetFullPath(Path.Combine(_tempDir, "subdir/../file.txt"));
        Assert.Equal(expected, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_WithDotDotSegments_CanonicalizesPath()
    {
        var inputPath = Path.Combine(_tempDir, "a", "..", "b", "file.txt");
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_ForCanonicalization
        {
            TaskEnvironment = env,
            InputPath = inputPath,
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        Assert.DoesNotContain("..", task.Result);
    }

    #endregion

    #region UsesPathGetFullPath_IgnoresTaskEnv

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ImplementsIMultiThreadableTask()
    {
        var task = new Fixed.UsesPathGetFullPath_IgnoresTaskEnv();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_WithRelativePath_UsesProjectDirectory()
    {
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_IgnoresTaskEnv
        {
            TaskEnvironment = env,
            InputPath = "relative/file.txt",
            BuildEngine = new FakeBuildEngine()
        };
        bool result = task.Execute();

        Assert.True(result);
        var projectResolved = Path.GetFullPath(Path.Combine(_tempDir, "relative/file.txt"));
        Assert.Equal(projectResolved, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_WithAbsolutePath_ReturnsCorrectResult()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var env = CreateEnv();
        var task = new Fixed.UsesPathGetFullPath_IgnoresTaskEnv
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
