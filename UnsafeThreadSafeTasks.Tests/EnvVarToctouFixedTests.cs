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
/// Tests for the fixed EnvVarToctou task that uses TaskEnvironment
/// instead of process-global Environment to read environment variables,
/// eliminating the Time-Of-Check-to-Time-Of-Use race condition.
/// </summary>
public class EnvVarToctouFixedTests
{
    private const int ThreadCount = 32;

    #region Structural Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_ExtendsTask()
    {
        var task = new FixedIntermittent.EnvVarToctou();
        Assert.IsAssignableFrom<Microsoft.Build.Utilities.Task>(task);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.EnvVarToctou)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedIntermittent.EnvVarToctou)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_HasTaskEnvironmentProperty()
    {
        var prop = typeof(FixedIntermittent.EnvVarToctou)
            .GetProperty(nameof(FixedIntermittent.EnvVarToctou.TaskEnvironment));
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_VariableNameIsRequired()
    {
        var prop = typeof(FixedIntermittent.EnvVarToctou)
            .GetProperty(nameof(FixedIntermittent.EnvVarToctou.VariableName));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<RequiredAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_InitialValueIsOutput()
    {
        var prop = typeof(FixedIntermittent.EnvVarToctou)
            .GetProperty(nameof(FixedIntermittent.EnvVarToctou.InitialValue));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_FinalValueIsOutput()
    {
        var prop = typeof(FixedIntermittent.EnvVarToctou)
            .GetProperty(nameof(FixedIntermittent.EnvVarToctou.FinalValue));
        Assert.NotNull(prop);
        var attr = prop!.GetCustomAttribute<OutputAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_DefaultOutputsAreEmpty()
    {
        var task = new FixedIntermittent.EnvVarToctou();
        Assert.Equal(string.Empty, task.InitialValue);
        Assert.Equal(string.Empty, task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_HasNoStaticState()
    {
        var staticFields = typeof(FixedIntermittent.EnvVarToctou)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.Empty(staticFields);
    }

    #endregion

    #region Functional Tests

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_ExecuteReturnsTrue()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("TEST_VAR", "test_value");
        var task = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env,
            VariableName = "TEST_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_ReadsFromTaskEnvironment()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("MY_VAR", "hello_world");
        var task = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env,
            VariableName = "MY_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        task.Execute();

        Assert.Equal("hello_world", task.InitialValue);
        Assert.Equal("hello_world", task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_BothReadsAlwaysMatch()
    {
        var env = new TaskEnvironment();
        env.SetEnvironmentVariable("STABLE_VAR", "stable_value");
        var task = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env,
            VariableName = "STABLE_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        task.Execute();

        Assert.Equal(task.InitialValue, task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_MissingVarReturnsEmptyString()
    {
        var env = new TaskEnvironment();
        var task = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env,
            VariableName = "NONEXISTENT_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.InitialValue);
        Assert.Equal(string.Empty, task.FinalValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_DifferentEnvironments_ProduceDifferentResults()
    {
        var env1 = new TaskEnvironment();
        env1.SetEnvironmentVariable("TEST_VAR", "value_a");

        var env2 = new TaskEnvironment();
        env2.SetEnvironmentVariable("TEST_VAR", "value_b");

        var task1 = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env1,
            VariableName = "TEST_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        var task2 = new FixedIntermittent.EnvVarToctou
        {
            TaskEnvironment = env2,
            VariableName = "TEST_VAR",
            BuildEngine = new EnvVarToctouTestBuildEngine()
        };

        task1.Execute();
        task2.Execute();

        Assert.Equal("value_a", task1.InitialValue);
        Assert.Equal("value_b", task2.InitialValue);
        Assert.NotEqual(task1.InitialValue, task2.InitialValue);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_DoesNotUseProcessEnvironment()
    {
        var varName = $"TOCTOU_FIXED_TEST_{Guid.NewGuid():N}";
        var originalValue = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "process_value");

            var env = new TaskEnvironment();
            env.SetEnvironmentVariable(varName, "task_env_value");

            var task = new FixedIntermittent.EnvVarToctou
            {
                TaskEnvironment = env,
                VariableName = varName,
                BuildEngine = new EnvVarToctouTestBuildEngine()
            };

            task.Execute();

            Assert.Equal("task_env_value", task.InitialValue);
            Assert.NotEqual("process_value", task.InitialValue);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
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
    public void EnvVarToctou_Fixed_ConcurrentEachReadsOwnEnvironment(int iteration)
    {
        _ = iteration;
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string Expected, string Initial, string Final)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var myValue = $"thread_val_{i}";
            var t = new Thread(() =>
            {
                var env = new TaskEnvironment();
                env.SetEnvironmentVariable("TOCTOU_VAR", myValue);
                var task = new FixedIntermittent.EnvVarToctou
                {
                    TaskEnvironment = env,
                    VariableName = "TOCTOU_VAR",
                    BuildEngine = new EnvVarToctouTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add((myValue, task.InitialValue, task.FinalValue));
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        Assert.Equal(ThreadCount, results.Count);
        foreach (var (expected, initial, final) in results)
        {
            Assert.Equal(expected, initial);
            Assert.Equal(expected, final);
            Assert.Equal(initial, final);
        }

        var distinctResults = results.Select(r => r.Initial).Distinct().ToList();
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
    public void EnvVarToctou_Fixed_ConcurrentNoToctouDetected(int iteration)
    {
        _ = iteration;
        var barrier = new Barrier(ThreadCount);
        var toctouDetected = new ConcurrentBag<bool>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var myValue = $"thread_val_{i}";
            var t = new Thread(() =>
            {
                var env = new TaskEnvironment();
                env.SetEnvironmentVariable("TOCTOU_VAR", myValue);
                var task = new FixedIntermittent.EnvVarToctou
                {
                    TaskEnvironment = env,
                    VariableName = "TOCTOU_VAR",
                    BuildEngine = new EnvVarToctouTestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                toctouDetected.Add(task.InitialValue != task.FinalValue);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // Fixed: no thread should ever see a mismatch between initial and final reads
        Assert.DoesNotContain(true, toctouDetected);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Fixed")]
    public void EnvVarToctou_Fixed_ConcurrentNoExceptions()
    {
        var barrier = new Barrier(ThreadCount);
        var exceptions = new ConcurrentBag<Exception>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var myValue = $"exception_test_{i}";
            var t = new Thread(() =>
            {
                try
                {
                    var env = new TaskEnvironment();
                    env.SetEnvironmentVariable("TOCTOU_VAR", myValue);
                    var task = new FixedIntermittent.EnvVarToctou
                    {
                        TaskEnvironment = env,
                        VariableName = "TOCTOU_VAR",
                        BuildEngine = new EnvVarToctouTestBuildEngine()
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
    public void EnvVarToctou_Fixed_ConcurrentWithProcessEnvWriters_StillConsistent()
    {
        var varName = $"TOCTOU_CONCURRENT_{Guid.NewGuid():N}";
        int readerCount = ThreadCount / 2;
        int writerCount = ThreadCount / 2;
        var barrier = new Barrier(readerCount + writerCount);
        var toctouDetected = new ConcurrentBag<bool>();
        var stop = new ManualResetEventSlim(false);
        var threads = new List<Thread>();

        try
        {
            Environment.SetEnvironmentVariable(varName, "initial");

            // Writer threads: continuously modify the process-global env var
            for (int i = 0; i < writerCount; i++)
            {
                var myValue = $"writer_{i}";
                var t = new Thread(() =>
                {
                    barrier.SignalAndWait();
                    while (!stop.IsSet)
                    {
                        Environment.SetEnvironmentVariable(varName, myValue);
                        Thread.Yield();
                    }
                });
                threads.Add(t);
            }

            // Reader threads: run the fixed TOCTOU task with TaskEnvironment
            for (int i = 0; i < readerCount; i++)
            {
                var myValue = $"reader_{i}";
                var t = new Thread(() =>
                {
                    var env = new TaskEnvironment();
                    env.SetEnvironmentVariable(varName, myValue);
                    var task = new FixedIntermittent.EnvVarToctou
                    {
                        TaskEnvironment = env,
                        VariableName = varName,
                        BuildEngine = new EnvVarToctouTestBuildEngine()
                    };
                    barrier.SignalAndWait();
                    task.Execute();
                    toctouDetected.Add(task.InitialValue != task.FinalValue);
                });
                threads.Add(t);
            }

            foreach (var t in threads) t.Start();

            // Wait for reader threads to finish, then signal writers to stop
            foreach (var t in threads.Skip(writerCount)) t.Join();
            stop.Set();
            foreach (var t in threads.Take(writerCount)) t.Join();

            // Fixed: even with concurrent process-global env writers, the task reads
            // from its own TaskEnvironment snapshot and never sees a mismatch
            Assert.DoesNotContain(true, toctouDetected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    #endregion

    #region Contrast: Unsafe vs Fixed

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void EnvVarToctou_Unsafe_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeIntermittent.EnvVarToctou)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void EnvVarToctou_Fixed_ImplementsIMultiThreadableTask_Contrast()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedIntermittent.EnvVarToctou)));
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void EnvVarToctou_Unsafe_DoesNotHaveTaskEnvironment()
    {
        var prop = typeof(UnsafeIntermittent.EnvVarToctou)
            .GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "IntermittentViolation")]
    [Trait("Target", "Contrast")]
    public void EnvVarToctou_Fixed_HasTaskEnvironment_Contrast()
    {
        var prop = typeof(FixedIntermittent.EnvVarToctou)
            .GetProperty("TaskEnvironment");
        Assert.NotNull(prop);
    }

    #endregion
}

internal class EnvVarToctouTestBuildEngine : IBuildEngine
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
