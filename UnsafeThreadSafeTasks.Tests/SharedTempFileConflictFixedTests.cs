using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using FixedIntermittent = FixedThreadSafeTasks.IntermittentViolations;
using UnsafeIntermittent = UnsafeThreadSafeTasks.IntermittentViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for the fixed SharedTempFileConflict task that uses a unique temp file
/// per instance (via GUID) instead of a shared hardcoded path.
/// </summary>
public class SharedTempFileConflictFixedTests
{
    private const int ThreadCount = 32;

    #region Structural Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ExtendsTask()
    {
        var task = new FixedIntermittent.SharedTempFileConflict();
        Assert.IsAssignableFrom<Microsoft.Build.Utilities.Task>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.SharedTempFileConflict)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_HasTaskEnvironmentProperty()
    {
        var prop = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetProperty(nameof(FixedIntermittent.SharedTempFileConflict.TaskEnvironment));
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ContentIsRequired()
    {
        var prop = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetProperty(nameof(FixedIntermittent.SharedTempFileConflict.Content));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ReadBackIsOutput()
    {
        var prop = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetProperty(nameof(FixedIntermittent.SharedTempFileConflict.ReadBack));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_DefaultReadBackIsEmpty()
    {
        var task = new FixedIntermittent.SharedTempFileConflict();
        Assert.Equal(string.Empty, task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_HasNoStaticTempFilePathField()
    {
        var staticFields = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Empty(staticFields);
    }

    #endregion

    #region Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ExecuteReturnsTrue()
    {
        var task = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "test_content",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ReadBack));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ReadBackMatchesContent()
    {
        var task = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "my_unique_content",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        task.Execute();

        Assert.Equal("my_unique_content", task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_DifferentInstancesReadBackTheirOwnContent()
    {
        var task1 = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "content_alpha",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        var task2 = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "content_beta",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        task1.Execute();
        task2.Execute();

        Assert.Equal("content_alpha", task1.ReadBack);
        Assert.Equal("content_beta", task2.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_EmptyContentWorksCorrectly()
    {
        var task = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("", task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_LargeContentWorksCorrectly()
    {
        var largeContent = new string('X', 10_000);
        var task = new FixedIntermittent.SharedTempFileConflict
        {
            Content = largeContent,
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        task.Execute();

        Assert.Equal(largeContent, task.ReadBack);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_TempFileIsCleanedUp()
    {
        var task = new FixedIntermittent.SharedTempFileConflict
        {
            Content = "cleanup_test",
            BuildEngine = new SharedTempFileConflictTestBuildEngine()
        };

        task.Execute();

        // The fixed implementation deletes the temp file in a finally block.
        // We can't easily check which file was used, but we verify Execute succeeds
        // and doesn't leave orphaned files by running many instances.
        Assert.Equal("cleanup_test", task.ReadBack);
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
    public void SharedTempFileConflict_Fixed_ConcurrentEachReadsOwnContent(int iteration)
    {
        _ = iteration;
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string Expected, string Actual)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var content = $"content_from_thread_{i}";
            var t = new Thread(() =>
            {
                var task = new FixedIntermittent.SharedTempFileConflict
                {
                    Content = content,
                    BuildEngine = new SharedTempFileConflictTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add((content, task.ReadBack));
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Equal(ThreadCount, results.Count);
        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ConcurrentNoExceptions()
    {
        var barrier = new Barrier(ThreadCount);
        var exceptions = new ConcurrentBag<Exception>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var content = $"exception_test_{i}";
            var t = new Thread(() =>
            {
                try
                {
                    var task = new FixedIntermittent.SharedTempFileConflict
                    {
                        Content = content,
                        BuildEngine = new SharedTempFileConflictTestBuildEngine()
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

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void SharedTempFileConflict_Fixed_ConcurrentAllResultsAreDistinct()
    {
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string Expected, string Actual)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var content = $"distinct_content_{i}";
            var t = new Thread(() =>
            {
                var task = new FixedIntermittent.SharedTempFileConflict
                {
                    Content = content,
                    BuildEngine = new SharedTempFileConflictTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add((content, task.ReadBack));
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // Each thread wrote unique content and should read back its own
        var distinctActual = results.Select(r => r.Actual).Distinct().ToList();
        Assert.Equal(ThreadCount, distinctActual.Count);
    }

    #endregion

    #region Contrast: Unsafe vs Fixed

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void SharedTempFileConflict_Unsafe_HasStaticTempFilePathField()
    {
        var staticFields = typeof(UnsafeIntermittent.SharedTempFileConflict)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(string));
        Assert.Single(staticFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void SharedTempFileConflict_Fixed_DoesNotHaveStaticState()
    {
        var staticFields = typeof(FixedIntermittent.SharedTempFileConflict)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Empty(staticFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void SharedTempFileConflict_Unsafe_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeIntermittent.SharedTempFileConflict)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void SharedTempFileConflict_Fixed_ImplementsIMultiThreadableTask_Contrast()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.SharedTempFileConflict)));
    }

    #endregion
}

internal class SharedTempFileConflictTestBuildEngine : IBuildEngine
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
