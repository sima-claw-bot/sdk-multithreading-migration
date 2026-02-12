using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests;

public class PathViolationTests : IDisposable
{
    private readonly string _tempDir;

    public PathViolationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PathViolationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region RelativePathToDirectoryExists

    [Fact]
    public void RelativePathToDirectoryExists_ExecuteReturnsTrue()
    {
        var task = new RelativePathToDirectoryExists { InputPath = _tempDir, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToDirectoryExists_AbsolutePath_ReturnsCorrectResult()
    {
        var task = new RelativePathToDirectoryExists { InputPath = _tempDir, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToDirectoryExists_NonExistentDir_ReturnsFalse()
    {
        var task = new RelativePathToDirectoryExists
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

        var task = new RelativePathToDirectoryExists
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

    #region RelativePathToFileExists

    [Fact]
    public void RelativePathToFileExists_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "hello");

        var task = new RelativePathToFileExists { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToFileExists_ExistingFile_ReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "hello");

        var task = new RelativePathToFileExists { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("True", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_NonExistentFile_ReturnsFalse()
    {
        var task = new RelativePathToFileExists
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

        var task = new RelativePathToFileExists
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

    #region RelativePathToFileStream

    [Fact]
    public void RelativePathToFileStream_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "stream_test.txt");
        File.WriteAllText(filePath, "first line\nsecond line");

        var task = new RelativePathToFileStream { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToFileStream_ReadsFirstLine()
    {
        var filePath = Path.Combine(_tempDir, "stream_test.txt");
        File.WriteAllText(filePath, "first line\nsecond line");

        var task = new RelativePathToFileStream { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("first line", task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_RelativePath_ResolvesAgainstCwd()
    {
        var filePath = Path.Combine(_tempDir, "rel_stream.txt");
        File.WriteAllText(filePath, "relative content");

        var task = new RelativePathToFileStream
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

    #region RelativePathToXDocument

    [Fact]
    public void RelativePathToXDocument_ExecuteReturnsTrue()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<Root><Child /></Root>");

        var task = new RelativePathToXDocument { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToXDocument_ReturnsRootElementName()
    {
        var filePath = Path.Combine(_tempDir, "test.xml");
        File.WriteAllText(filePath, "<MyRoot><Child /></MyRoot>");

        var task = new RelativePathToXDocument { InputPath = filePath, BuildEngine = new FakeBuildEngine() };
        task.Execute();
        Assert.Equal("MyRoot", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_RelativePath_ResolvesAgainstCwd()
    {
        var filePath = Path.Combine(_tempDir, "rel_doc.xml");
        File.WriteAllText(filePath, "<Project />");

        var task = new RelativePathToXDocument
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

    #region UsesPathGetFullPath_AttributeOnly

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ExecuteReturnsTrue()
    {
        var task = new UsesPathGetFullPath_AttributeOnly
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
        var task = new UsesPathGetFullPath_AttributeOnly
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
        var task = new UsesPathGetFullPath_AttributeOnly
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

    #region UsesPathGetFullPath_ForCanonicalization

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ExecuteReturnsTrue()
    {
        var task = new UsesPathGetFullPath_ForCanonicalization
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

        var task = new UsesPathGetFullPath_ForCanonicalization
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
        var task = new UsesPathGetFullPath_ForCanonicalization
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

    #region UsesPathGetFullPath_IgnoresTaskEnv

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ImplementsIMultiThreadableTask()
    {
        var task = new UsesPathGetFullPath_IgnoresTaskEnv();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ExecuteReturnsTrue()
    {
        var task = new UsesPathGetFullPath_IgnoresTaskEnv
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

        var task = new UsesPathGetFullPath_IgnoresTaskEnv
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
        var task = new UsesPathGetFullPath_IgnoresTaskEnv { TaskEnvironment = taskEnv };
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
