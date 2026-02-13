using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for unsafe ComplexViolation tasks (batch 2):
/// TaskAlpha05, TaskAlpha06, TaskAlpha07, TaskAlpha08.
/// These tests verify the CWD-dependent and static-state bugs present in the unsafe versions.
/// </summary>
public class UnsafeComplexViolationBatch2Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public UnsafeComplexViolationBatch2Tests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ucvb2test_{Guid.NewGuid():N}");
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

    #region TaskAlpha05 — CWD + static cache bugs

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = "somefile.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_ResultContainsRelativePath()
    {
        var uniquePath = $"dcv_contains_{Guid.NewGuid():N}.txt";
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Contains(uniquePath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_ResultIsAbsolutePath()
    {
        var uniquePath = $"dcv_abs_{Guid.NewGuid():N}.txt";
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_ResolvesAgainstProcessCwd()
    {
        ClearDictionaryCache();
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var uniquePath = $"dcv_cwd_{Guid.NewGuid():N}.txt";
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();

        // BUG: resolves against the process CWD via Path.GetFullPath
        Assert.Equal(Path.Combine(dir, uniquePath), task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_StaticCacheReturnsStaleCachedResults()
    {
        ClearDictionaryCache();
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var key = $"stale_{Guid.NewGuid():N}.txt";

        // First call caches the result under dir1
        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = key,
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task1.Execute();

        // Second call with different CWD gets stale cached result
        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = key,
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task2.Execute();

        // BUG: static cache returns stale result from first CWD
        Assert.Equal(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_HasStaticCacheField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha05)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.TaskAlpha05)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.TaskAlpha05)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha05).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void DictionaryCacheViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha05();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    #endregion

    #region TaskAlpha06 — CWD captured at event fire time via static event

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "somefile.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_ResultContainsRelativePath()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "myfile.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_ResultIsAbsolutePath()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "relative.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_ResolvesAgainstProcessCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "test.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();

        // BUG: resolves against the process CWD at event fire time
        Assert.Equal(Path.Combine(dir, "test.txt"), task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "file.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "file.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task2.Execute();

        // BUG: results depend on CWD at event fire time
        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath);
        Assert.StartsWith(dir2, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_HandlersAccumulate_StaticEventGrows()
    {
        var eventField = typeof(UnsafeComplex.TaskAlpha06)
            .GetField("PathResolved", BindingFlags.NonPublic | BindingFlags.Static);

        var task1 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "acc1.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "acc2.txt",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task2.Execute();

        var eventValue = eventField?.GetValue(null) as Delegate;
        Assert.NotNull(eventValue);
        // After at least two executions, multiple handlers are accumulated
        Assert.True(eventValue!.GetInvocationList().Length >= 2);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_HasStaticEventField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha06)
            .GetField("PathResolved", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.TaskAlpha06)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.TaskAlpha06)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha06).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void EventHandlerViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha06();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    #endregion

    #region TaskAlpha07 — static Lazy<T> captures CWD at first-access time

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "mytool.exe",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_ResultContainsToolName()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "compiler.exe",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.EndsWith("compiler.exe", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_ResultContainsToolsDirectory()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool.exe",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("tools", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_StaticLazyReturnsSameBaseForAllCalls()
    {
        var task1 = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool1.exe",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool2.exe",
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task2.Execute();

        // BUG: both use the same base directory from the static Lazy<T>
        var baseDir1 = Path.GetDirectoryName(task1.ResolvedToolPath);
        var baseDir2 = Path.GetDirectoryName(task2.ResolvedToolPath);
        Assert.Equal(baseDir1, baseDir2);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_HasStaticLazyField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha07)
            .GetField("CachedToolPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.TaskAlpha07)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.TaskAlpha07)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha07).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LazyInitializationViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha07();
        Assert.Equal(string.Empty, task.ToolName);
        Assert.Equal(string.Empty, task.ResolvedToolPath);
    }

    #endregion

    #region TaskAlpha08 — deferred LINQ evaluation uses CWD at materialization time

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file1.txt", "file2.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_ResolvesAllPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "a.txt", "b.txt", "c.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(3, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_ResultsAreAbsolutePaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "src", "bin" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();

        foreach (var p in task.ResolvedPaths)
        {
            Assert.True(Path.IsPathRooted(p));
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_ResolvesAgainstProcessCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "src", "bin" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();

        // BUG: Path.GetFullPath in LINQ pipeline resolves against CWD
        foreach (var resolved in task.ResolvedPaths)
        {
            Assert.StartsWith(dir, resolved);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task2.Execute();

        // BUG: deferred LINQ evaluation uses CWD at materialization time
        Assert.NotEqual(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
        Assert.StartsWith(dir1, task1.ResolvedPaths[0]);
        Assert.StartsWith(dir2, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_EmptyInput_ReturnsEmptyArray()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = Array.Empty<string>(),
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_FiltersBlankPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file1.txt", "", "  ", "file2.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_DeduplicatesPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "same.txt", "same.txt" },
            BuildEngine = new UnsafeBatch2TestBuildEngine()
        };
        task.Execute();
        Assert.Single(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.TaskAlpha08)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.TaskAlpha08)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha08).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    public void LinqPipelineViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha08();
        Assert.Empty(task.RelativePaths);
        Assert.Empty(task.ResolvedPaths);
    }

    #endregion

    #region Cross-cutting structural checks for all batch 2 types

    public static IEnumerable<object[]> Batch2TaskTypes()
    {
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha05) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha06) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha07) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha08) };
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.ComplexViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "2")]
    [MemberData(nameof(Batch2TaskTypes))]
    public void Batch2Type_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    #endregion

    #region Helpers

    private static void ClearDictionaryCache()
    {
        var cacheField = typeof(UnsafeComplex.TaskAlpha05)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as ConcurrentDictionary<string, string>;
        cache?.Clear();
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for UnsafeComplexViolation batch 2 tests.
/// </summary>
internal class UnsafeBatch2TestBuildEngine : IBuildEngine
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
