#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

/// <summary>
/// Tests verifying the unsafe CWD-dependent behavior of PathViolation tasks.
/// These tests explicitly manipulate the process CWD to demonstrate that
/// relative paths resolve against it rather than a per-task project directory.
/// Each test saves/restores CWD via try/finally to minimize cross-test interference.
/// </summary>
[Trait("Category", "PathViolation")]
[Trait("Target", "Unsafe")]
[Collection("CwdSensitive")]
public class UnsafePathViolationBehaviorTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"pvbehavior_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static StubBuildEngine Engine() => new();

    #region CWD-dependent resolution: Directory.Exists

    [Fact]
    public void RelativePathToDirectoryExists_CwdSwitch_ResultChangesWithCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var subName = "cwdtest_subdir";
        Directory.CreateDirectory(Path.Combine(dir1, subName));

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta01 { InputPath = subName, BuildEngine = Engine() };
            task1.Execute();
            Assert.Equal("True", task1.Result);

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta01 { InputPath = subName, BuildEngine = Engine() };
            task2.Execute();
            Assert.Equal("False", task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToDirectoryExists_DotPath_ResolvesToCwd()
    {
        var dir = CreateTempDir();
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta01 { InputPath = ".", BuildEngine = Engine() };
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToDirectoryExists_DotDotPath_ResolvesToParentOfCwd()
    {
        var dir = CreateTempDir();
        var sub = Path.Combine(dir, "child");
        Directory.CreateDirectory(sub);

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(sub);
            var task = new TaskZeta01 { InputPath = "..", BuildEngine = Engine() };
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: File.Exists

    [Fact]
    public void RelativePathToFileExists_CwdSwitch_ResultChangesWithCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "cwdtest_file.txt";
        File.WriteAllText(Path.Combine(dir1, fileName), "content");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            task1.Execute();
            Assert.Equal("True", task1.Result);

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            task2.Execute();
            Assert.Equal("False", task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToFileExists_NestedRelativePath_ResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        var nested = Path.Combine(dir, "a", "b");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "deep.txt"), "data");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta02
            {
                InputPath = Path.Combine("a", "b", "deep.txt"),
                BuildEngine = Engine()
            };
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: FileStream

    [Fact]
    public void RelativePathToFileStream_CwdSwitch_ReadsDifferentFiles()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "cwdtest_stream.txt";
        File.WriteAllText(Path.Combine(dir1, fileName), "from_dir1");
        File.WriteAllText(Path.Combine(dir2, fileName), "from_dir2");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
            task1.Execute();
            Assert.Equal("from_dir1", task1.Result);

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
            task2.Execute();
            Assert.Equal("from_dir2", task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToFileStream_NonExistentRelativePath_Throws()
    {
        // Use a unique name that won't exist anywhere
        var fileName = $"nonexistent_{Guid.NewGuid():N}.txt";
        var task = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    [Fact]
    public void RelativePathToFileStream_SingleLineFile_ReturnsEntireLine()
    {
        var dir = CreateTempDir();
        var fileName = "singleline.txt";
        File.WriteAllText(Path.Combine(dir, fileName), "only line here");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
            task.Execute();
            Assert.Equal("only line here", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: XDocument.Load

    [Fact]
    public void RelativePathToXDocument_CwdSwitch_ReadsDifferentFiles()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "cwdtest.xml";
        File.WriteAllText(Path.Combine(dir1, fileName), "<Alpha />");
        File.WriteAllText(Path.Combine(dir2, fileName), "<Beta />");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
            task1.Execute();
            Assert.Equal("Alpha", task1.Result);

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
            task2.Execute();
            Assert.Equal("Beta", task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToXDocument_NonExistentRelativePath_Throws()
    {
        var fileName = $"nonexistent_{Guid.NewGuid():N}.xml";
        var task = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    [Fact]
    public void RelativePathToXDocument_EmptyRootElement_ReturnsLocalName()
    {
        var dir = CreateTempDir();
        var fileName = "empty_root.xml";
        File.WriteAllText(Path.Combine(dir, fileName), "<EmptyRoot />");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
            task.Execute();
            Assert.Equal("EmptyRoot", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: Path.GetFullPath (AttributeOnly)

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_CwdSwitch_ResolvesToDifferentPaths()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var relPath = "some_file.txt";

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta05 { InputPath = relPath, BuildEngine = Engine() };
            task1.Execute();

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta05 { InputPath = relPath, BuildEngine = Engine() };
            task2.Execute();

            Assert.NotEqual(task1.Result, task2.Result);
            Assert.StartsWith(dir1, task1.Result, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, task2.Result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_AbsolutePath_UnaffectedByCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var absPath = Path.Combine(dir1, "file.txt");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta05 { InputPath = absPath, BuildEngine = Engine() };
            task1.Execute();

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta05 { InputPath = absPath, BuildEngine = Engine() };
            task2.Execute();

            Assert.Equal(task1.Result, task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: Path.GetFullPath (ForCanonicalization)

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_CwdSwitch_ResolvesToDifferentPaths()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var relPath = Path.Combine("a", "..", "file.txt");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            task1.Execute();

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            task2.Execute();

            Assert.NotEqual(task1.Result, task2.Result);
            Assert.DoesNotContain("..", task1.Result);
            Assert.DoesNotContain("..", task2.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_MultipleSegments_FullyCanonicalizes()
    {
        var dir = CreateTempDir();
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var relPath = Path.Combine("a", "b", "..", "..", "c", "file.txt");
            var task = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            task.Execute();

            Assert.DoesNotContain("..", task.Result);
            Assert.EndsWith(Path.Combine("c", "file.txt"), task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region CWD-dependent resolution: IgnoresTaskEnv (IMultiThreadableTask with bug)

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_CwdSwitch_IgnoresProjectDirectory()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var projectDir = CreateTempDir();
        var relPath = "file.txt";

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta07
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            task1.Execute();

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta07
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            task2.Execute();

            // Same ProjectDirectory, different CWDs → different results (the bug)
            Assert.NotEqual(task1.Result, task2.Result);
            Assert.StartsWith(dir1, task1.Result, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, task2.Result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projectDir, task1.Result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projectDir, task2.Result, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_AbsoluteInput_CwdIrrelevant()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var absPath = Path.Combine(dir1, "abs_file.txt");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = new TaskZeta07
            {
                InputPath = absPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                BuildEngine = Engine()
            };
            task1.Execute();

            Directory.SetCurrentDirectory(dir2);
            var task2 = new TaskZeta07
            {
                InputPath = absPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                BuildEngine = Engine()
            };
            task2.Execute();

            Assert.Equal(task1.Result, task2.Result);
            Assert.Equal(absPath, task1.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_DefaultTaskEnvironment_HasEmptyProjectDir()
    {
        var task = new TaskZeta07();
        Assert.NotNull(task.TaskEnvironment);
        Assert.Equal(string.Empty, task.TaskEnvironment.ProjectDirectory);
    }

    #endregion

    #region Structural: All 7 unsafe tasks

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllUnsafeTasks_ExtendMSBuildTask(Type taskType)
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
    public void AllUnsafeTasks_InputPathIsRequired(Type taskType)
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
    public void AllUnsafeTasks_ResultIsOutput(Type taskType)
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
    public void AllUnsafeTasks_DefaultInputPathIsEmpty(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType)!;
        var val = (string)taskType.GetProperty("InputPath")!.GetValue(instance)!;
        Assert.Equal(string.Empty, val);
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    [InlineData(typeof(TaskZeta07))]
    public void AllUnsafeTasks_DefaultResultIsEmpty(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType)!;
        var val = (string)taskType.GetProperty("Result")!.GetValue(instance)!;
        Assert.Equal(string.Empty, val);
    }

    [Theory]
    [InlineData(typeof(TaskZeta01))]
    [InlineData(typeof(TaskZeta02))]
    [InlineData(typeof(TaskZeta03))]
    [InlineData(typeof(TaskZeta04))]
    [InlineData(typeof(TaskZeta05))]
    [InlineData(typeof(TaskZeta06))]
    public void NonIMultiThreadableTasks_LackTaskEnvironmentProperty(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
        Assert.Null(taskType.GetProperty("TaskEnvironment"));
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_IsOnlyIMultiThreadableTaskAmongUnsafe()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(TaskZeta07)));
    }

    #endregion

    #region Cross-task consistency: same relative path, same CWD → consistent behavior

    [Fact]
    public void FileExistsAndFileStream_SameRelativePath_ConsistentBehavior()
    {
        var dir = CreateTempDir();
        var fileName = "consistent_test.txt";
        File.WriteAllText(Path.Combine(dir, fileName), "hello");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);

            var existsTask = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            existsTask.Execute();
            Assert.Equal("True", existsTask.Result);

            var streamTask = new TaskZeta03 { InputPath = fileName, BuildEngine = Engine() };
            streamTask.Execute();
            Assert.Equal("hello", streamTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void FileExistsAndXDocument_SameRelativePath_ConsistentBehavior()
    {
        var dir = CreateTempDir();
        var fileName = "consistent.xml";
        File.WriteAllText(Path.Combine(dir, fileName), "<Root />");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);

            var existsTask = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            existsTask.Execute();
            Assert.Equal("True", existsTask.Result);

            var xdocTask = new TaskZeta04 { InputPath = fileName, BuildEngine = Engine() };
            xdocTask.Execute();
            Assert.Equal("Root", xdocTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void GetFullPathTasks_SameRelativeInput_ProduceSameResult()
    {
        var dir = CreateTempDir();
        var relPath = Path.Combine("sub", "file.txt");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);

            var attrTask = new TaskZeta05 { InputPath = relPath, BuildEngine = Engine() };
            attrTask.Execute();

            var canonTask = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            canonTask.Execute();

            var envTask = new TaskZeta07
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
                BuildEngine = Engine()
            };
            envTask.Execute();

            // All three resolve the same relative path against CWD (since none contains "..")
            Assert.Equal(attrTask.Result, canonTask.Result);
            Assert.Equal(attrTask.Result, envTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region Path format edge cases

    [Fact]
    public void RelativePathToDirectoryExists_TrailingSeparator_StillWorks()
    {
        var dir = CreateTempDir();
        var subName = "trailing_sep";
        Directory.CreateDirectory(Path.Combine(dir, subName));

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta01
            {
                InputPath = subName + Path.DirectorySeparatorChar,
                BuildEngine = Engine()
            };
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void RelativePathToFileExists_PathWithSpaces_ResolvesCorrectly()
    {
        var dir = CreateTempDir();
        var fileName = "file with spaces.txt";
        File.WriteAllText(Path.Combine(dir, fileName), "content");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta02 { InputPath = fileName, BuildEngine = Engine() };
            task.Execute();
            Assert.Equal("True", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_DotPath_ResolvesToCwd()
    {
        var dir = CreateTempDir();
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            var task = new TaskZeta05 { InputPath = ".", BuildEngine = Engine() };
            task.Execute();
            Assert.Equal(dir, task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_DotDotFromSubdir_ResolvesCorrectly()
    {
        var dir = CreateTempDir();
        var sub = Path.Combine(dir, "child");
        Directory.CreateDirectory(sub);

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(sub);
            var relPath = Path.Combine("..", "file.txt");
            var task = new TaskZeta06 { InputPath = relPath, BuildEngine = Engine() };
            task.Execute();

            Assert.Equal(Path.Combine(dir, "file.txt"), task.Result);
            Assert.DoesNotContain("..", task.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion
}

internal class StubBuildEngine : IBuildEngine
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
