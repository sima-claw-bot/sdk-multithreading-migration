#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.PathViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

/// <summary>
/// Reflection-based tests that verify structural contracts and instantiation behavior
/// across all 7 PathViolation unsafe tasks. These tests ensure every task type
/// follows the expected patterns for MSBuild task design and exhibits the
/// documented unsafe CWD-dependent path resolution.
/// </summary>
[Trait("Category", "PathViolation")]
[Trait("Target", "Unsafe")]
public class UnsafePathViolationReflectionTests : IDisposable
{
    private static readonly Type[] AllUnsafePathViolationTypes =
    {
        typeof(TaskZeta01),
        typeof(TaskZeta02),
        typeof(TaskZeta03),
        typeof(TaskZeta04),
        typeof(TaskZeta05),
        typeof(TaskZeta06),
        typeof(TaskZeta07),
    };

    private static readonly Type[] NonMultiThreadableTypes =
    {
        typeof(TaskZeta01),
        typeof(TaskZeta02),
        typeof(TaskZeta03),
        typeof(TaskZeta04),
        typeof(TaskZeta05),
        typeof(TaskZeta06),
    };

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
        var dir = Path.Combine(Path.GetTempPath(), $"pvreflect_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static ReflectionTestBuildEngine Engine() => new();

    #region Parameterless constructor and default state

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_HaveParameterlessConstructor(Type taskType)
    {
        var ctor = taskType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(ctor);
        Assert.True(ctor!.IsPublic);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_CanBeInstantiatedViaActivator(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
        Assert.IsType(taskType, instance);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_InputPath_IsStringType(Type taskType)
    {
        var prop = taskType.GetProperty("InputPath");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_Result_IsStringType(Type taskType)
    {
        var prop = taskType.GetProperty("Result");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_InputPath_IsReadWrite(Type taskType)
    {
        var prop = taskType.GetProperty("InputPath")!;
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_Result_IsReadWrite(Type taskType)
    {
        var prop = taskType.GetProperty("Result")!;
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_AreInPathViolationsNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.PathViolations", taskType.Namespace);
    }

    [Theory]
    [MemberData(nameof(AllUnsafeTypeData))]
    public void AllTasks_ArePublicClasses(Type taskType)
    {
        Assert.True(taskType.IsPublic);
        Assert.True(taskType.IsClass);
        Assert.False(taskType.IsAbstract);
    }

    #endregion

    #region IMultiThreadableTask contract

    [Theory]
    [MemberData(nameof(NonMultiThreadableTypeData))]
    public void NonMultiThreadableTasks_DoNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    public void IgnoresTaskEnv_HasTaskEnvironmentProperty_WithCorrectType()
    {
        var prop = typeof(TaskZeta07).GetProperty("TaskEnvironment");
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
        Assert.True(prop.CanRead);
        Assert.True(prop.CanWrite);
    }

    [Fact]
    public void IgnoresTaskEnv_DefaultTaskEnvironment_IsNotNull()
    {
        var task = new TaskZeta07();
        Assert.NotNull(task.TaskEnvironment);
    }

    [Fact]
    public void IgnoresTaskEnv_DefaultTaskEnvironment_HasEmptyProjectDirectory()
    {
        var task = new TaskZeta07();
        Assert.Equal(string.Empty, task.TaskEnvironment.ProjectDirectory);
    }

    #endregion

    #region Execute via reflection — all tasks return true with valid input

    [Fact]
    public void RelativePathToDirectoryExists_ExecuteViaReflection_ReturnsTrue()
    {
        var dir = CreateTempDir();
        var task = CreateTask<TaskZeta01>(dir);
        Assert.True(InvokeExecute(task));
        Assert.Equal("True", GetResult(task));
    }

    [Fact]
    public void RelativePathToFileExists_ExecuteViaReflection_ReturnsTrue()
    {
        var dir = CreateTempDir();
        var filePath = Path.Combine(dir, "test.txt");
        File.WriteAllText(filePath, "content");
        var task = CreateTask<TaskZeta02>(filePath);
        Assert.True(InvokeExecute(task));
        Assert.Equal("True", GetResult(task));
    }

    [Fact]
    public void RelativePathToFileStream_ExecuteViaReflection_ReadsFirstLine()
    {
        var dir = CreateTempDir();
        var filePath = Path.Combine(dir, "test.txt");
        File.WriteAllText(filePath, "first line\nsecond line");
        var task = CreateTask<TaskZeta03>(filePath);
        Assert.True(InvokeExecute(task));
        Assert.Equal("first line", GetResult(task));
    }

    [Fact]
    public void RelativePathToXDocument_ExecuteViaReflection_ReturnsRootName()
    {
        var dir = CreateTempDir();
        var filePath = Path.Combine(dir, "test.xml");
        File.WriteAllText(filePath, "<TestRoot />");
        var task = CreateTask<TaskZeta04>(filePath);
        Assert.True(InvokeExecute(task));
        Assert.Equal("TestRoot", GetResult(task));
    }

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_ExecuteViaReflection_ResolvesPath()
    {
        var dir = CreateTempDir();
        var absPath = Path.Combine(dir, "file.txt");
        var task = CreateTask<TaskZeta05>(absPath);
        Assert.True(InvokeExecute(task));
        Assert.Equal(Path.GetFullPath(absPath), GetResult(task));
    }

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_ExecuteViaReflection_CanonicalizesPath()
    {
        var dir = CreateTempDir();
        var inputPath = Path.Combine(dir, "a", "..", "file.txt");
        var task = CreateTask<TaskZeta06>(inputPath);
        Assert.True(InvokeExecute(task));
        Assert.DoesNotContain("..", GetResult(task));
        Assert.Equal(Path.Combine(dir, "file.txt"), GetResult(task));
    }

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ExecuteViaReflection_UsesCwdNotProjectDir()
    {
        var projectDir = CreateTempDir();
        var task = new TaskZeta07
        {
            InputPath = "file.txt",
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
            BuildEngine = Engine()
        };
        Assert.True(task.Execute());

        // Bug: resolves against CWD, not project directory
        var cwdResolved = Path.GetFullPath("file.txt");
        Assert.Equal(cwdResolved, task.Result);
        Assert.DoesNotContain(projectDir, task.Result, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Exactly 7 PathViolation unsafe task types exist

    [Fact]
    public void PathViolationsNamespace_ContainsExactly7Types()
    {
        var assembly = typeof(TaskZeta01).Assembly;
        var types = Array.FindAll(
            assembly.GetExportedTypes(),
            t => t.Namespace == "UnsafeThreadSafeTasks.PathViolations" && typeof(Task).IsAssignableFrom(t));
        Assert.Equal(7, types.Length);
    }

    [Fact]
    public void PathViolationsNamespace_ContainsExpectedTypes()
    {
        var assembly = typeof(TaskZeta01).Assembly;
        var types = Array.FindAll(
            assembly.GetExportedTypes(),
            t => t.Namespace == "UnsafeThreadSafeTasks.PathViolations" && typeof(Task).IsAssignableFrom(t));

        var typeNames = new HashSet<string>(Array.ConvertAll(types, t => t.Name));
        Assert.Contains("TaskZeta01", typeNames);
        Assert.Contains("TaskZeta02", typeNames);
        Assert.Contains("TaskZeta03", typeNames);
        Assert.Contains("TaskZeta04", typeNames);
        Assert.Contains("TaskZeta05", typeNames);
        Assert.Contains("TaskZeta06", typeNames);
        Assert.Contains("TaskZeta07", typeNames);
    }

    [Fact]
    public void OnlyOneTaskImplementsIMultiThreadableTask()
    {
        var assembly = typeof(TaskZeta01).Assembly;
        var types = Array.FindAll(
            assembly.GetExportedTypes(),
            t => t.Namespace == "UnsafeThreadSafeTasks.PathViolations"
                 && typeof(Task).IsAssignableFrom(t)
                 && typeof(IMultiThreadableTask).IsAssignableFrom(t));
        Assert.Single(types);
        Assert.Equal(typeof(TaskZeta07), types[0]);
    }

    #endregion

    #region Helper methods and member data

    public static IEnumerable<object[]> AllUnsafeTypeData()
    {
        foreach (var t in AllUnsafePathViolationTypes)
            yield return new object[] { t };
    }

    public static IEnumerable<object[]> NonMultiThreadableTypeData()
    {
        foreach (var t in NonMultiThreadableTypes)
            yield return new object[] { t };
    }

    private T CreateTask<T>(string inputPath) where T : Task, new()
    {
        var task = new T();
        task.BuildEngine = Engine();
        task.GetType().GetProperty("InputPath")!.SetValue(task, inputPath);
        return task;
    }

    private static bool InvokeExecute(Task task) => task.Execute();

    private static string GetResult(Task task) =>
        (string)task.GetType().GetProperty("Result")!.GetValue(task)!;

    #endregion
}

internal class ReflectionTestBuildEngine : IBuildEngine
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
