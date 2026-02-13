using System;
using System.Collections;
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
/// Tests for unsafe IntermittentViolation tasks (batch 1):
/// TaskDelta01, TaskDelta02, TaskDelta07, TaskDelta08.
/// These tests verify structural properties, property attributes, and the specific
/// process-global state bugs present in the unsafe versions.
/// </summary>
public class UnsafeIntermittentViolationBatch1Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public UnsafeIntermittentViolationBatch1Tests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"uivb1test_{Guid.NewGuid():N}");
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

    #region TaskDelta01 — Property and attribute tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_ProjectDirectory_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta01).GetProperty("ProjectDirectory");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_RelativePath_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta01).GetProperty("RelativePath");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_ResolvedPath_HasOutputAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta01).GetProperty("ResolvedPath");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(OutputAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_DefaultProperties_AreEmptyStrings()
    {
        var task = new UnsafeIntermittent.TaskDelta01();
        Assert.Equal(string.Empty, task.ProjectDirectory);
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_ResolvesNestedRelativePath()
    {
        var dir = CreateTempDir();
        var relativePath = Path.Combine("a", "b", "c", "file.txt");
        var task = new UnsafeIntermittent.TaskDelta01
        {
            ProjectDirectory = dir,
            RelativePath = relativePath,
            BuildEngine = new IntermittentBatch1BuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void CwdRaceCondition_MutatesProcessCwdToProjectDirectory()
    {
        var dir = CreateTempDir();
        var before = Environment.CurrentDirectory;

        var task = new UnsafeIntermittent.TaskDelta01
        {
            ProjectDirectory = dir,
            RelativePath = "file.txt",
            BuildEngine = new IntermittentBatch1BuildEngine()
        };
        task.Execute();

        // Verify the process-global CWD was changed (the bug)
        Assert.NotEqual(before, Environment.CurrentDirectory);
        Assert.Equal(dir, Environment.CurrentDirectory);
    }

    #endregion

    #region TaskDelta02 — Property and attribute tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_VariableName_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta02).GetProperty("VariableName");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_InitialValue_HasOutputAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta02).GetProperty("InitialValue");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(OutputAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_FinalValue_HasOutputAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta02).GetProperty("FinalValue");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(OutputAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_DefaultProperties_AreEmptyStrings()
    {
        var task = new UnsafeIntermittent.TaskDelta02();
        Assert.Equal(string.Empty, task.VariableName);
        Assert.Equal(string.Empty, task.InitialValue);
        Assert.Equal(string.Empty, task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_CapturesEnvVarValue()
    {
        var varName = $"TOCTOU_B1_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(varName, "hello_world");
            var task = new UnsafeIntermittent.TaskDelta02
            {
                VariableName = varName,
                BuildEngine = new IntermittentBatch1BuildEngine()
            };

            task.Execute();

            Assert.Equal("hello_world", task.InitialValue);
            Assert.Equal("hello_world", task.FinalValue);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_UnsetVariable_ReturnsEmptyStrings()
    {
        var varName = $"TOCTOU_UNSET_{Guid.NewGuid():N}";
        var task = new UnsafeIntermittent.TaskDelta02
        {
            VariableName = varName,
            BuildEngine = new IntermittentBatch1BuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.InitialValue);
        Assert.Equal(string.Empty, task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void EnvVarToctou_AlwaysReturnsTrue()
    {
        var varName = $"TOCTOU_RET_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(varName, "value");
            var task = new UnsafeIntermittent.TaskDelta02
            {
                VariableName = varName,
                BuildEngine = new IntermittentBatch1BuildEngine()
            };

            Assert.True(task.Execute());
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    #endregion

    #region TaskDelta07 — Property and attribute tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_Content_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta07).GetProperty("Content");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_ReadBack_HasOutputAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta07).GetProperty("ReadBack");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(OutputAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_DefaultProperties_AreEmptyStrings()
    {
        var task = new UnsafeIntermittent.TaskDelta07();
        Assert.Equal(string.Empty, task.Content);
        Assert.Equal(string.Empty, task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_WritesAndReadsContent()
    {
        var task = new UnsafeIntermittent.TaskDelta07
        {
            Content = "batch1_test_content",
            BuildEngine = new IntermittentBatch1BuildEngine()
        };

        Assert.True(task.Execute());
        Assert.Equal("batch1_test_content", task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_StaticTempFilePathField_Exists()
    {
        var field = typeof(UnsafeIntermittent.TaskDelta07)
            .GetField("TempFilePath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
        Assert.True(field.IsInitOnly);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_TempFilePath_IsInTempDirectory()
    {
        var field = typeof(UnsafeIntermittent.TaskDelta07)
            .GetField("TempFilePath", BindingFlags.NonPublic | BindingFlags.Static);
        var path = field!.GetValue(null) as string;
        Assert.NotNull(path);
        Assert.StartsWith(Path.GetTempPath(), path!);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void SharedTempFileConflict_SequentialCallsShareSamePath()
    {
        var task1 = new UnsafeIntermittent.TaskDelta07
        {
            Content = "first",
            BuildEngine = new IntermittentBatch1BuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeIntermittent.TaskDelta07
        {
            Content = "second",
            BuildEngine = new IntermittentBatch1BuildEngine()
        };
        task2.Execute();

        // Second call overwrites the first because they use the same static file path
        Assert.Equal("second", task2.ReadBack);
    }

    #endregion

    #region TaskDelta08 — Property and attribute tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_ProjectDirectory_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta08).GetProperty("ProjectDirectory");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_RelativePath_HasRequiredAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta08).GetProperty("RelativePath");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(RequiredAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_ResolvedPath_HasOutputAttribute()
    {
        var prop = typeof(UnsafeIntermittent.TaskDelta08).GetProperty("ResolvedPath");
        Assert.NotNull(prop);
        Assert.True(Attribute.IsDefined(prop!, typeof(OutputAttribute)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_DefaultProperties_AreEmptyStrings()
    {
        var task = new UnsafeIntermittent.TaskDelta08();
        Assert.Equal(string.Empty, task.ProjectDirectory);
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_PathCacheField_IsPlainDictionary()
    {
        var field = typeof(UnsafeIntermittent.TaskDelta08)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        var cache = field!.GetValue(null);
        Assert.IsType<Dictionary<string, string>>(cache);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_ResolvesFullPath()
    {
        var dir = CreateTempDir();
        var relativePath = $"unique_b1_{Guid.NewGuid():N}\\output.cs";
        var task = new UnsafeIntermittent.TaskDelta08
        {
            ProjectDirectory = dir,
            RelativePath = relativePath,
            BuildEngine = new IntermittentBatch1BuildEngine()
        };

        Assert.True(task.Execute());
        var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void StaticCachePathCollision_SecondCallWithDifferentDir_ReturnsCachedResult()
    {
        var relativePath = $"cached_b1_{Guid.NewGuid():N}\\file.cs";
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new UnsafeIntermittent.TaskDelta08
        {
            ProjectDirectory = dir1,
            RelativePath = relativePath,
            BuildEngine = new IntermittentBatch1BuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeIntermittent.TaskDelta08
        {
            ProjectDirectory = dir2,
            RelativePath = relativePath,
            BuildEngine = new IntermittentBatch1BuildEngine()
        };
        task2.Execute();

        // BUG: cache key is only relative path, so dir2 gets dir1's result
        Assert.Equal(task1.ResolvedPath, task2.ResolvedPath);
        Assert.Contains(dir1, task2.ResolvedPath);
        Assert.DoesNotContain(dir2, task2.ResolvedPath);
    }

    #endregion

    #region Cross-cutting structural checks for all batch 1 types

    public static IEnumerable<object[]> Batch1TaskTypes()
    {
        yield return new object[] { typeof(UnsafeIntermittent.TaskDelta01) };
        yield return new object[] { typeof(UnsafeIntermittent.TaskDelta02) };
        yield return new object[] { typeof(UnsafeIntermittent.TaskDelta07) };
        yield return new object[] { typeof(UnsafeIntermittent.TaskDelta08) };
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.IntermittentViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_HasPublicParameterlessConstructor(Type taskType)
    {
        var ctor = taskType.GetConstructor(Type.EmptyTypes);
        Assert.NotNull(ctor);
        Assert.True(ctor!.IsPublic);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for IntermittentViolation batch 1 tests.
/// </summary>
internal class IntermittentBatch1BuildEngine : IBuildEngine
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
