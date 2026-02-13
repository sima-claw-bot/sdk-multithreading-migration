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
/// Tests for ComplexViolation unsafe tasks (batch 2):
/// TaskAlpha05, TaskAlpha06, TaskAlpha07, TaskAlpha08.
/// Covers structural checks, property attributes, default values, and CWD-dependent behavior.
/// </summary>
public class ComplexViolationBatch2Tests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _savedCwd;

    public ComplexViolationBatch2Tests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CVBatch2_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _savedCwd = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_savedCwd);
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    #region TaskAlpha05 — structural checks

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.TaskAlpha05();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.TaskAlpha05();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha05).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_RelativePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha05)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha05.RelativePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ResolvedPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha05)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha05.ResolvedPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha05();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.TaskAlpha05),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region TaskAlpha05 — behavior tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = "somefile.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ProducesNonEmptyResult()
    {
        // Use a unique path to avoid static cache interference
        var uniquePath = $"dcv_nonempty_{Guid.NewGuid():N}.txt";
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.NotEmpty(task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ResultIsAbsolutePath()
    {
        var uniquePath = $"dcv_abs_{Guid.NewGuid():N}.txt";
        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_ResolvesAgainstCwd()
    {
        Directory.SetCurrentDirectory(_tempDir);
        var uniquePath = $"dcv_cwd_{Guid.NewGuid():N}.txt";

        var task = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = uniquePath,
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();

        // BUG: resolves against the process CWD via Path.GetFullPath
        Assert.Equal(Path.Combine(_tempDir, uniquePath), task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_StaticCache_ReturnsStaleCachedResults()
    {
        // Clear static cache via reflection
        var cacheField = typeof(UnsafeComplex.TaskAlpha05)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as ConcurrentDictionary<string, string>;
        cache?.Clear();

        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        // Use a unique key to avoid interference with other tests
        var key = $"stale_{Guid.NewGuid():N}.txt";

        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = key,
            BuildEngine = new Batch2TestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.TaskAlpha05
        {
            RelativePath = key,
            BuildEngine = new Batch2TestBuildEngine()
        };
        task2.Execute();

        // BUG: static cache returns stale result from first CWD
        Assert.Equal(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_HasStaticCacheField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha05)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    #endregion

    #region TaskAlpha06 — structural checks

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.TaskAlpha06();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.TaskAlpha06();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha06).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_RelativePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha06)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha06.RelativePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ResolvedPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha06)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha06.ResolvedPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha06();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.TaskAlpha06),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region TaskAlpha06 — behavior tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "somefile.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ProducesNonEmptyResult()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "output.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.NotEmpty(task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ResultIsAbsolutePath()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "relative.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ResultContainsRelativePath()
    {
        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "myfile.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_ResolvesAgainstCwd()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "test.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();

        // BUG: resolves against the process CWD at event fire time
        Assert.Equal(Path.Combine(_tempDir, "test.txt"), task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_CwdChangeBetweenCalls_ProducesDifferentResults()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "file.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "file.txt",
            BuildEngine = new Batch2TestBuildEngine()
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
    public void EventHandlerViolation_HandlersAccumulate_StaticEventGrows()
    {
        // The static event accumulates handlers across invocations
        var eventField = typeof(UnsafeComplex.TaskAlpha06)
            .GetField("PathResolved", BindingFlags.NonPublic | BindingFlags.Static);

        var task1 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "acc1.txt",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.TaskAlpha06
        {
            RelativePath = "acc2.txt",
            BuildEngine = new Batch2TestBuildEngine()
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
    public void EventHandlerViolation_HasStaticEventField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha06)
            .GetField("PathResolved", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    #endregion

    #region TaskAlpha07 — structural checks

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.TaskAlpha07();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.TaskAlpha07();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha07).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ToolNameHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha07)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha07.ToolName));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ResolvedToolPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha07)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha07.ResolvedToolPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha07();
        Assert.Equal(string.Empty, task.ToolName);
        Assert.Equal(string.Empty, task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.TaskAlpha07),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region TaskAlpha07 — behavior tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "mytool.exe",
            BuildEngine = new Batch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ProducesNonEmptyResult()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool.exe",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.NotEmpty(task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ResultContainsToolName()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "compiler.exe",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.EndsWith("compiler.exe", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_ResultContainsToolsDirectory()
    {
        var task = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool.exe",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("tools", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_StaticLazyReturnsSameBaseForAllCalls()
    {
        var task1 = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool1.exe",
            BuildEngine = new Batch2TestBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.TaskAlpha07
        {
            ToolName = "tool2.exe",
            BuildEngine = new Batch2TestBuildEngine()
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
    public void LazyInitializationViolation_HasStaticLazyField()
    {
        var field = typeof(UnsafeComplex.TaskAlpha07)
            .GetField("CachedToolPath", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    #endregion

    #region TaskAlpha08 — structural checks

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.TaskAlpha08();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.TaskAlpha08();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha08).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_RelativePathsHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha08)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha08.RelativePaths));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_ResolvedPathsHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.TaskAlpha08)
            .GetProperty(nameof(UnsafeComplex.TaskAlpha08.ResolvedPaths));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.TaskAlpha08();
        Assert.Empty(task.RelativePaths);
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.TaskAlpha08),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region TaskAlpha08 — behavior tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file1.txt", "file2.txt" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_ResolvesAllPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "a.txt", "b.txt", "c.txt" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(3, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_ResultsAreAbsolutePaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "src", "bin" },
            BuildEngine = new Batch2TestBuildEngine()
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
    public void LinqPipelineViolation_ResolvesAgainstCwd()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "src", "bin" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();

        // BUG: Path.GetFullPath in LINQ pipeline resolves against CWD
        foreach (var resolved in task.ResolvedPaths)
        {
            Assert.StartsWith(_tempDir, resolved);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_CwdChangeBetweenCalls_ProducesDifferentResults()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new Batch2TestBuildEngine()
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
    public void LinqPipelineViolation_EmptyInput_ReturnsEmptyArray()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = Array.Empty<string>(),
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_FiltersBlankPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "file1.txt", "", "  ", "file2.txt" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_DeduplicatesPaths()
    {
        var task = new UnsafeComplex.TaskAlpha08
        {
            RelativePaths = new[] { "same.txt", "same.txt" },
            BuildEngine = new Batch2TestBuildEngine()
        };
        task.Execute();
        Assert.Single(task.ResolvedPaths);
    }

    #endregion

    #region All batch 2 types — common structural checks

    public static IEnumerable<object[]> Batch2ComplexViolationTypes()
    {
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha05) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha06) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha07) };
        yield return new object[] { typeof(UnsafeComplex.TaskAlpha08) };
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.ComplexViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(Batch2ComplexViolationTypes))]
    public void Batch2Type_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for batch 2 tests.
/// </summary>
internal class Batch2TestBuildEngine : IBuildEngine
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
