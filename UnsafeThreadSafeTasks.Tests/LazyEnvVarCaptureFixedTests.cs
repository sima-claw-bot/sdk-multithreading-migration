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
/// Tests for the fixed LazyEnvVarCapture task that uses TaskEnvironment
/// instead of a static Lazy&lt;string&gt; to read environment variables.
/// </summary>
public class LazyEnvVarCaptureFixedTests
{
    private const int ThreadCount = 32;

    #region Structural Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_ExtendsTask()
    {
        var task = new FixedIntermittent.LazyEnvVarCapture();
        Assert.IsAssignableFrom<Microsoft.Build.Utilities.Task>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.LazyEnvVarCapture)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedIntermittent.LazyEnvVarCapture)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_HasTaskEnvironmentProperty()
    {
        var prop = typeof(FixedIntermittent.LazyEnvVarCapture)
            .GetProperty(nameof(FixedIntermittent.LazyEnvVarCapture.TaskEnvironment));
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_ResultIsOutput()
    {
        var prop = typeof(FixedIntermittent.LazyEnvVarCapture)
            .GetProperty(nameof(FixedIntermittent.LazyEnvVarCapture.Result));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_DefaultResultIsEmpty()
    {
        var task = new FixedIntermittent.LazyEnvVarCapture();
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_HasNoStaticLazyField()
    {
        var lazyFields = typeof(FixedIntermittent.LazyEnvVarCapture)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Lazy<>));
        Assert.Empty(lazyFields);
    }

    #endregion

    #region Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_ExecuteReturnsTrue()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("MY_SETTING", "test_value");
        var task = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_ReadsFromTaskEnvironment()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("MY_SETTING", "hello_world");
        var task = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        task.Execute();

        Assert.Equal("hello_world", task.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_MissingVarReturnsEmptyString()
    {
        var env = new TaskEnvironment();
        var task = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_DifferentEnvironments_ProduceDifferentResults()
    {
        var env1 = new TaskEnvironment();
        env1.SetEnvironmentVariable("MY_SETTING", "value_a");

        var env2 = new TaskEnvironment();
        env2.SetEnvironmentVariable("MY_SETTING", "value_b");

        var task1 = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env1,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        var task2 = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env2,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        task1.Execute();
        task2.Execute();

        Assert.Equal("value_a", task1.Result);
        Assert.Equal("value_b", task2.Result);
        Assert.NotEqual(task1.Result, task2.Result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_EachExecutionReadsCurrentValue()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("MY_SETTING", "first");

        var task = new FixedIntermittent.LazyEnvVarCapture
        {
            TaskEnvironment = env,
            BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
        };

        task.Execute();
        Assert.Equal("first", task.Result);

        env.SetEnvironmentVariable("MY_SETTING", "second");

        task.Execute();
        Assert.Equal("second", task.Result);
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
    public void LazyEnvVarCapture_Fixed_ConcurrentEachReadsOwnEnvironment(int iteration)
    {
        _ = iteration;
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string Expected, string Actual)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var myValue = $"thread_val_{i}";
            var t = new Thread(() =>
            {
                var env = new TaskEnvironment();
                env.SetEnvironmentVariable("MY_SETTING", myValue);
                var task = new FixedIntermittent.LazyEnvVarCapture
                {
                    TaskEnvironment = env,
                    BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add((myValue, task.Result));
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

        var distinctResults = results.Select(r => r.Actual).Distinct().ToList();
        Assert.Equal(ThreadCount, distinctResults.Count);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void LazyEnvVarCapture_Fixed_DoesNotUseProcessEnvironment()
    {
        var varName = "MY_SETTING";
        var originalValue = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "process_value");

            var env = new TaskEnvironment();
            env.SetEnvironmentVariable(varName, "task_env_value");

            var task = new FixedIntermittent.LazyEnvVarCapture
            {
                TaskEnvironment = env,
                BuildEngine = new LazyEnvVarCaptureTestBuildEngine()
            };

            task.Execute();

            Assert.Equal("task_env_value", task.Result);
            Assert.NotEqual("process_value", task.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion

    #region Contrast: Unsafe vs Fixed

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void LazyEnvVarCapture_Unsafe_HasStaticLazyField()
    {
        var lazyFields = typeof(UnsafeIntermittent.LazyEnvVarCapture)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(Lazy<>));
        Assert.Single(lazyFields);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void LazyEnvVarCapture_Fixed_DoesNotImplementStaticState()
    {
        var staticFields = typeof(FixedIntermittent.LazyEnvVarCapture)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Empty(staticFields);
    }

    #endregion
}

internal class LazyEnvVarCaptureTestBuildEngine : IBuildEngine
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