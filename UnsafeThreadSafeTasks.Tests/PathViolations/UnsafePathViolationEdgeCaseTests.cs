#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

/// <summary>
/// Edge-case and error-handling tests for PathViolation unsafe tasks.
/// Verifies behavior with empty paths, special characters, concurrent CWD mutations,
/// and validates that the unsafe pattern (CWD-dependent resolution) is consistent
/// across repeated executions and edge-case inputs.
/// </summary>
[Trait("Category", "PathViolation")]
[Trait("Target", "Unsafe")]
public class UnsafePathViolationEdgeCaseTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"pvedge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static EdgeCaseBuildEngine Engine() => new();

    #region Execute idempotency — calling Execute twice yields same result

    [Fact]
    public void RelativePathToDirectoryExists_ExecuteTwice_SameResult()
    {
        var dir = CreateTempDir();
        var task = new RelativePathToDirectoryExists { InputPath = dir, BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_ExecuteTwice_SameResult()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "idem.txt");
        File.WriteAllText(file, "data");
        var task = new RelativePathToFileExists { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void RelativePathToFileStream_ExecuteTwice_SameResult()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "idem_stream.txt");
        File.WriteAllText(file, "line1\nline2");
        var task = new RelativePathToFileStream { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_ExecuteTwice_SameResult()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "idem.xml");
        File.WriteAllText(file, "<Root />");
        var task = new RelativePathToXDocument { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ExecuteTwice_SameResult()
    {
        var task = new UsesPathGetFullPath_AttributeOnly { InputPath = "test.txt", BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ExecuteTwice_SameResult()
    {
        var input = Path.Combine("a", "..", "b.txt");
        var task = new UsesPathGetFullPath_ForCanonicalization { InputPath = input, BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ExecuteTwice_SameResult()
    {
        var projectDir = CreateTempDir();
        var task = new UsesPathGetFullPath_IgnoresTaskEnv
        {
            InputPath = "test.txt",
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
            BuildEngine = Engine()
        };
        task.Execute();
        var first = task.Result;
        task.Execute();
        Assert.Equal(first, task.Result);
    }

    #endregion

    #region InputPath can be reassigned between executions

    [Fact]
    public void RelativePathToDirectoryExists_ReassignInputPath_ResultUpdates()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var task = new RelativePathToDirectoryExists { InputPath = dir1, BuildEngine = Engine() };
        task.Execute();
        Assert.Equal("True", task.Result);

        task.InputPath = Path.Combine(dir2, "nonexistent");
        task.Execute();
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_ReassignInputPath_ResultUpdates()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "exists.txt");
        File.WriteAllText(file, "x");

        var task = new RelativePathToFileExists { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        Assert.Equal("True", task.Result);

        task.InputPath = Path.Combine(dir, "gone.txt");
        task.Execute();
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ReassignInputPath_ResultUpdates()
    {
        var task = new UsesPathGetFullPath_AttributeOnly { InputPath = "a.txt", BuildEngine = Engine() };
        task.Execute();
        var first = task.Result;

        task.InputPath = "b.txt";
        task.Execute();
        Assert.NotEqual(first, task.Result);
        Assert.EndsWith("b.txt", task.Result);
    }

    #endregion

    #region Absolute paths bypass CWD dependency

    [Theory]
    [InlineData(typeof(RelativePathToDirectoryExists))]
    [InlineData(typeof(RelativePathToFileExists))]
    [InlineData(typeof(UsesPathGetFullPath_AttributeOnly))]
    [InlineData(typeof(UsesPathGetFullPath_ForCanonicalization))]
    [InlineData(typeof(UsesPathGetFullPath_IgnoresTaskEnv))]
    public void AbsoluteInputPath_SameResultRegardlessOfCwd(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var targetDir = CreateTempDir();
        var targetFile = Path.Combine(targetDir, "abs_test.txt");
        File.WriteAllText(targetFile, "content");

        string absInput;
        if (taskType == typeof(RelativePathToDirectoryExists))
            absInput = targetDir;
        else
            absInput = targetFile;

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir1);
            var task1 = CreateAndExecuteTask(taskType, absInput);

            Directory.SetCurrentDirectory(dir2);
            var task2 = CreateAndExecuteTask(taskType, absInput);

            Assert.Equal(task1, task2);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region All tasks always return true from Execute

    [Theory]
    [InlineData(typeof(RelativePathToDirectoryExists))]
    [InlineData(typeof(RelativePathToFileExists))]
    [InlineData(typeof(UsesPathGetFullPath_AttributeOnly))]
    [InlineData(typeof(UsesPathGetFullPath_ForCanonicalization))]
    [InlineData(typeof(UsesPathGetFullPath_IgnoresTaskEnv))]
    public void Execute_AlwaysReturnsTrue_EvenForNonExistentPaths(Type taskType)
    {
        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = Engine();
        taskType.GetProperty("InputPath")!.SetValue(task, "nonexistent_path_12345");
        if (task is IMultiThreadableTask mt)
            mt.TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() };

        Assert.True(task.Execute());
    }

    [Fact]
    public void RelativePathToDirectoryExists_NonExistentPath_ReturnsTrue_WithFalseResult()
    {
        var task = new RelativePathToDirectoryExists
        {
            InputPath = "this_dir_does_not_exist_" + Guid.NewGuid().ToString("N"),
            BuildEngine = Engine()
        };
        Assert.True(task.Execute());
        Assert.Equal("False", task.Result);
    }

    [Fact]
    public void RelativePathToFileExists_NonExistentPath_ReturnsTrue_WithFalseResult()
    {
        var task = new RelativePathToFileExists
        {
            InputPath = "this_file_does_not_exist_" + Guid.NewGuid().ToString("N"),
            BuildEngine = Engine()
        };
        Assert.True(task.Execute());
        Assert.Equal("False", task.Result);
    }

    #endregion

    #region FileStream and XDocument throw on non-existent relative paths

    [Fact]
    public void RelativePathToFileStream_NonExistentPath_ThrowsFileNotFoundException()
    {
        var task = new RelativePathToFileStream
        {
            InputPath = "nonexistent_" + Guid.NewGuid().ToString("N") + ".txt",
            BuildEngine = Engine()
        };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    [Fact]
    public void RelativePathToXDocument_NonExistentPath_ThrowsFileNotFoundException()
    {
        var task = new RelativePathToXDocument
        {
            InputPath = "nonexistent_" + Guid.NewGuid().ToString("N") + ".xml",
            BuildEngine = Engine()
        };
        Assert.ThrowsAny<Exception>(() => task.Execute());
    }

    #endregion

    #region Empty file content edge cases

    [Fact]
    public void RelativePathToFileStream_EmptyFile_ReturnsEmptyString()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "empty.txt");
        File.WriteAllText(file, string.Empty);

        var task = new RelativePathToFileStream { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_NestedElements_ReturnsRootOnly()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "nested.xml");
        File.WriteAllText(file, "<Root><Child><GrandChild /></Child></Root>");

        var task = new RelativePathToXDocument { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        Assert.Equal("Root", task.Result);
    }

    [Fact]
    public void RelativePathToXDocument_WithAttributes_ReturnsRootLocalName()
    {
        var dir = CreateTempDir();
        var file = Path.Combine(dir, "attrs.xml");
        File.WriteAllText(file, "<Root attr1=\"val1\" attr2=\"val2\" />");

        var task = new RelativePathToXDocument { InputPath = file, BuildEngine = Engine() };
        task.Execute();
        Assert.Equal("Root", task.Result);
    }

    #endregion

    #region IgnoresTaskEnv: TaskEnvironment is settable but ignored

    [Fact]
    public void IgnoresTaskEnv_DifferentProjectDirs_SameRelativePath_SameResult()
    {
        var projDir1 = CreateTempDir();
        var projDir2 = CreateTempDir();
        var relPath = "test.txt";

        var task1 = new UsesPathGetFullPath_IgnoresTaskEnv
        {
            InputPath = relPath,
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir1 },
            BuildEngine = Engine()
        };
        task1.Execute();

        var task2 = new UsesPathGetFullPath_IgnoresTaskEnv
        {
            InputPath = relPath,
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir2 },
            BuildEngine = Engine()
        };
        task2.Execute();

        // Both resolve against CWD, ignoring different ProjectDirectories
        Assert.Equal(task1.Result, task2.Result);
    }

    [Fact]
    public void IgnoresTaskEnv_NullProjectDirectory_StillExecutes()
    {
        var task = new UsesPathGetFullPath_IgnoresTaskEnv
        {
            InputPath = "file.txt",
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = null! },
            BuildEngine = Engine()
        };
        Assert.True(task.Execute());
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    public void IgnoresTaskEnv_TaskEnvironmentCanBeReassigned()
    {
        var task = new UsesPathGetFullPath_IgnoresTaskEnv();
        var env1 = new TaskEnvironment { ProjectDirectory = "/dir1" };
        var env2 = new TaskEnvironment { ProjectDirectory = "/dir2" };

        task.TaskEnvironment = env1;
        Assert.Same(env1, task.TaskEnvironment);

        task.TaskEnvironment = env2;
        Assert.Same(env2, task.TaskEnvironment);
    }

    #endregion

    #region MSBuild attribute presence on all tasks

    private static readonly Type[] AllPathViolationTaskTypes =
    {
        typeof(RelativePathToDirectoryExists),
        typeof(RelativePathToFileExists),
        typeof(RelativePathToFileStream),
        typeof(RelativePathToXDocument),
        typeof(UsesPathGetFullPath_AttributeOnly),
        typeof(UsesPathGetFullPath_ForCanonicalization),
        typeof(UsesPathGetFullPath_IgnoresTaskEnv),
    };

    [Theory]
    [MemberData(nameof(AllPathViolationTypeData))]
    public void AllTasks_InheritFromMSBuildTask(Type taskType)
    {
        Assert.True(typeof(Task).IsAssignableFrom(taskType));
        Assert.True(typeof(ITask).IsAssignableFrom(taskType));
    }

    [Theory]
    [MemberData(nameof(AllPathViolationTypeData))]
    public void AllTasks_HaveBuildEngineProperty(Type taskType)
    {
        var instance = (Task)Activator.CreateInstance(taskType)!;
        instance.BuildEngine = Engine();
        Assert.NotNull(instance.BuildEngine);
    }

    [Theory]
    [MemberData(nameof(AllPathViolationTypeData))]
    public void AllTasks_InputPathHasRequiredAttribute(Type taskType)
    {
        var attr = taskType.GetProperty("InputPath")!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Theory]
    [MemberData(nameof(AllPathViolationTypeData))]
    public void AllTasks_ResultHasOutputAttribute(Type taskType)
    {
        var attr = taskType.GetProperty("Result")!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    public static IEnumerable<object[]> AllPathViolationTypeData()
    {
        foreach (var t in AllPathViolationTaskTypes)
            yield return new object[] { t };
    }

    #endregion

    #region Helper methods

    private string CreateAndExecuteTask(Type taskType, string inputPath)
    {
        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = Engine();
        taskType.GetProperty("InputPath")!.SetValue(task, inputPath);
        if (task is IMultiThreadableTask mt)
            mt.TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() };
        task.Execute();
        return (string)taskType.GetProperty("Result")!.GetValue(task)!;
    }

    #endregion
}

internal class EdgeCaseBuildEngine : IBuildEngine
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
