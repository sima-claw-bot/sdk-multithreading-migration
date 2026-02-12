using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using FixedComplex = FixedThreadSafeTasks.ComplexViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for fixed ComplexViolation tasks (batch 2):
/// DictionaryCacheViolation, EventHandlerViolation, LazyInitializationViolation, LinqPipelineViolation.
/// </summary>
public class FixedComplexViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fcvtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    #region DictionaryCacheViolation — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.DictionaryCacheViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "somefile.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.DictionaryCacheViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "subdir\\file.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.DictionaryCacheViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            RelativePath = "file.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.DictionaryCacheViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            RelativePath = "file.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.DictionaryCacheViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.DictionaryCacheViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DictionaryCacheViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.DictionaryCacheViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                RelativePath = "file.txt",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedPath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.DictionaryCacheViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                RelativePath = "file.txt",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedPath;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region EventHandlerViolation — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.EventHandlerViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "somefile.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.EventHandlerViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "output.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_ResultContainsRelativePath()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.EventHandlerViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "myfile.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.EventHandlerViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            RelativePath = "file.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.EventHandlerViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            RelativePath = "file.txt",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.EventHandlerViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.EventHandlerViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void EventHandlerViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.EventHandlerViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                RelativePath = "file.txt",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedPath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.EventHandlerViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                RelativePath = "file.txt",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedPath;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region LazyInitializationViolation — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LazyInitializationViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ToolName = "mytool.exe",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LazyInitializationViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ToolName = "tool.exe",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.ResolvedToolPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tools", task.ResolvedToolPath);
        Assert.Contains("tool.exe", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.LazyInitializationViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            ToolName = "tool.exe",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.LazyInitializationViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            ToolName = "tool.exe",
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedToolPath, task2.ResolvedToolPath);
        Assert.StartsWith(dir1, task1.ResolvedToolPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedToolPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.LazyInitializationViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.LazyInitializationViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LazyInitializationViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.LazyInitializationViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                ToolName = "tool.exe",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedToolPath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.LazyInitializationViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                ToolName = "tool.exe",
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedToolPath;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region LinqPipelineViolation — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePaths = new[] { "file1.txt", "file2.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePaths = new[] { "a.txt", "b.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(2, task.ResolvedPaths.Length);
        foreach (var p in task.ResolvedPaths)
        {
            Assert.True(Path.IsPathRooted(p));
            Assert.StartsWith(projDir, p, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_EmptyInput_ReturnsEmptyArray()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePaths = Array.Empty<string>(),
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_FiltersWhitespaceEntries()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePaths = new[] { "file.txt", "  ", "", "other.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_DeduplicatesPaths()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePaths = new[] { "file.txt", "file.txt", "other.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.LinqPipelineViolation
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            RelativePaths = new[] { "file.txt" },
            BuildEngine = new FixedComplexTestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
        Assert.StartsWith(dir1, task1.ResolvedPaths[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.LinqPipelineViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.LinqPipelineViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void LinqPipelineViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string[]? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.LinqPipelineViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                RelativePaths = new[] { "file.txt" },
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedPaths;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.LinqPipelineViolation
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                RelativePaths = new[] { "file.txt" },
                BuildEngine = new FixedComplexTestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedPaths;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Single(result1!);
        Assert.Single(result2!);
        Assert.NotEqual(result1![0], result2![0]);
        Assert.StartsWith(dir1, result1![0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2![0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for FixedComplexViolation tests.
/// </summary>
internal class FixedComplexTestBuildEngine : Microsoft.Build.Framework.IBuildEngine
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
