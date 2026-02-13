using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using FixedIntermittent = FixedThreadSafeTasks.IntermittentViolations;
using UnsafeIntermittent = UnsafeThreadSafeTasks.IntermittentViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for the fixed StaticCachePathCollision task that uses TaskEnvironment
/// instead of a shared static Dictionary to resolve paths.
/// </summary>
public class StaticCachePathCollisionFixedTests : IDisposable
{
    private const int ThreadCount = 32;
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"scpctest_{Guid.NewGuid():N}");
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

    #region Structural Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ExtendsTask()
    {
        var task = new FixedIntermittent.StaticCachePathCollision();
        Assert.IsAssignableFrom<Microsoft.Build.Utilities.Task>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.StaticCachePathCollision)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_HasTaskEnvironmentProperty()
    {
        var prop = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetProperty(nameof(FixedIntermittent.StaticCachePathCollision.TaskEnvironment));
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_RelativePathIsRequired()
    {
        var prop = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetProperty(nameof(FixedIntermittent.StaticCachePathCollision.RelativePath));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ResolvedPathIsOutput()
    {
        var prop = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetProperty(nameof(FixedIntermittent.StaticCachePathCollision.ResolvedPath));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_DefaultResolvedPathIsEmpty()
    {
        var task = new FixedIntermittent.StaticCachePathCollision();
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_HasNoStaticDictionaryField()
    {
        var staticFields = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
        Assert.Empty(staticFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_HasNoStaticState()
    {
        var staticFields = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Empty(staticFields);
    }

    #endregion

    #region Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ExecuteReturnsTrue()
    {
        var dir = CreateTempDir();
        var env = new TaskEnvironment { ProjectDirectory = dir };
        var task = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env,
            RelativePath = "src\\file.cs",
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ResolvesPathCorrectly()
    {
        var dir = CreateTempDir();
        var relativePath = "src\\Program.cs";
        var env = new TaskEnvironment { ProjectDirectory = dir };
        var task = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_DifferentDirectories_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var relativePath = "src\\Program.cs";

        var env1 = new TaskEnvironment { ProjectDirectory = dir1 };
        var task1 = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env1,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };

        var env2 = new TaskEnvironment { ProjectDirectory = dir2 };
        var task2 = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env2,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };

        task1.Execute();
        task2.Execute();

        var expected1 = Path.GetFullPath(Path.Combine(dir1, relativePath));
        var expected2 = Path.GetFullPath(Path.Combine(dir2, relativePath));

        Assert.Equal(expected1, task1.ResolvedPath);
        Assert.Equal(expected2, task2.ResolvedPath);
        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_SameRelativePathDifferentDirs_NoCacheCollision()
    {
        var relativePath = "src\\Program.cs";
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        // First call
        var env1 = new TaskEnvironment { ProjectDirectory = dir1 };
        var task1 = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env1,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };
        task1.Execute();

        // Second call with same relative path but different directory
        var env2 = new TaskEnvironment { ProjectDirectory = dir2 };
        var task2 = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env2,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };
        task2.Execute();

        // Fixed: each resolves against its own project directory
        Assert.Contains(dir1, task1.ResolvedPath);
        Assert.Contains(dir2, task2.ResolvedPath);
        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_UsesTaskEnvironmentProjectDirectory()
    {
        var dir = CreateTempDir();
        var relativePath = "output\\result.txt";
        var env = new TaskEnvironment { ProjectDirectory = dir };
        var task = new FixedIntermittent.StaticCachePathCollision
        {
            TaskEnvironment = env,
            RelativePath = relativePath,
            BuildEngine = new StaticCachePathCollisionTestBuildEngine()
        };

        task.Execute();

        // The resolved path must start with the project directory from TaskEnvironment
        Assert.StartsWith(dir, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_RepeatedCallsAlwaysResolveCorrectly()
    {
        var dir = CreateTempDir();
        var env = new TaskEnvironment { ProjectDirectory = dir };

        for (int i = 0; i < 10; i++)
        {
            var relativePath = $"src\\file_{i}.cs";
            var task = new FixedIntermittent.StaticCachePathCollision
            {
                TaskEnvironment = env,
                RelativePath = relativePath,
                BuildEngine = new StaticCachePathCollisionTestBuildEngine()
            };

            task.Execute();

            var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
            Assert.Equal(expected, task.ResolvedPath);
        }
    }

    #endregion

    #region Concurrency Tests

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void StaticCachePathCollision_Fixed_ConcurrentDifferentDirs_EachResolvesCorrectly(int iteration)
    {
        _ = iteration;
        var relativePath = "src\\Program.cs";
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string ProjectDir, string ExpectedPath, string ResolvedPath)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var dir = CreateTempDir();
            var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
            var t = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir };
                var task = new FixedIntermittent.StaticCachePathCollision
                {
                    TaskEnvironment = env,
                    RelativePath = relativePath,
                    BuildEngine = new StaticCachePathCollisionTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add((dir, expected, task.ResolvedPath));
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Equal(ThreadCount, results.Count);
        foreach (var (projectDir, expectedPath, resolvedPath) in results)
        {
            Assert.Equal(expectedPath, resolvedPath);
            Assert.Contains(projectDir, resolvedPath);
        }

        // All results should be distinct since each uses a different project directory
        var distinctResults = results.Select(r => r.ResolvedPath).Distinct().ToList();
        Assert.Equal(ThreadCount, distinctResults.Count);
    }

    [Theory]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    public void StaticCachePathCollision_Fixed_ConcurrentSameRelativePath_NoCollision(int iteration)
    {
        _ = iteration;
        var relativePath = "src\\Program.cs";
        var barrier = new Barrier(ThreadCount);
        var mismatches = new ConcurrentBag<bool>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var dir = CreateTempDir();
            var expected = Path.GetFullPath(Path.Combine(dir, relativePath));
            var t = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir };
                var task = new FixedIntermittent.StaticCachePathCollision
                {
                    TaskEnvironment = env,
                    RelativePath = relativePath,
                    BuildEngine = new StaticCachePathCollisionTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                mismatches.Add(task.ResolvedPath != expected);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // Fixed: no thread should get a mismatched result
        Assert.DoesNotContain(true, mismatches);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void StaticCachePathCollision_Fixed_ConcurrentNoExceptions()
    {
        var relativePath = "src\\Program.cs";
        var barrier = new Barrier(ThreadCount);
        var exceptions = new ConcurrentBag<Exception>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var dir = CreateTempDir();
            var t = new Thread(() =>
            {
                try
                {
                    var env = new TaskEnvironment { ProjectDirectory = dir };
                    var task = new FixedIntermittent.StaticCachePathCollision
                    {
                        TaskEnvironment = env,
                        RelativePath = relativePath,
                        BuildEngine = new StaticCachePathCollisionTestBuildEngine()
                    };
                    barrier.SignalAndWait();
                    task.Execute();
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Empty(exceptions);
    }

    #endregion

    #region Contrast: Unsafe vs Fixed

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void StaticCachePathCollision_Unsafe_HasStaticDictionaryField()
    {
        var dictFields = typeof(UnsafeIntermittent.StaticCachePathCollision)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
        Assert.Single(dictFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void StaticCachePathCollision_Fixed_DoesNotHaveStaticDictionary()
    {
        var dictFields = typeof(FixedIntermittent.StaticCachePathCollision)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType.IsGenericType &&
                        f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>));
        Assert.Empty(dictFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void StaticCachePathCollision_Unsafe_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeIntermittent.StaticCachePathCollision)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void StaticCachePathCollision_Fixed_ImplementsIMultiThreadableTask_Contrast()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.StaticCachePathCollision)));
    }

    #endregion
}

internal class StaticCachePathCollisionTestBuildEngine : IBuildEngine
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
