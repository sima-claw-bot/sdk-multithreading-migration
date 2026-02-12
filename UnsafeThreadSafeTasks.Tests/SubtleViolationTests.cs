using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using FixedSubtle = FixedThreadSafeTasks.SubtleViolations;
using UnsafeSubtle = UnsafeThreadSafeTasks.SubtleViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class SubtleViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"svtest_{Guid.NewGuid():N}");
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

    #region SharedMutableStaticField

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedSubtle.SharedMutableStaticField))]
    public void SharedMutableStaticField_Fixed_EachTaskReturnsOwnValue(Type taskType)
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            taskType.GetProperty("InputValue")!.SetValue(task, "alpha");
            if (task is IMultiThreadableTask mt)
                mt.TaskEnvironment = new TaskEnvironment();
            barrier.SignalAndWait();
            task.Execute();
            result1 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
        });

        var t2 = new Thread(() =>
        {
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            taskType.GetProperty("InputValue")!.SetValue(task, "beta");
            if (task is IMultiThreadableTask mt)
                mt.TaskEnvironment = new TaskEnvironment();
            barrier.SignalAndWait();
            task.Execute();
            result2 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Fixed: each task reads back its own value
        Assert.Equal("alpha", result1);
        Assert.Equal("beta", result2);
    }

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.SharedMutableStaticField))]
    public void SharedMutableStaticField_Unsafe_ConcurrentRunsCauseCrossContamination(Type taskType)
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            taskType.GetProperty("InputValue")!.SetValue(task, "alpha");
            barrier.SignalAndWait();
            task.Execute();
            result1 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
        });

        var t2 = new Thread(() =>
        {
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            taskType.GetProperty("InputValue")!.SetValue(task, "beta");
            barrier.SignalAndWait();
            task.Execute();
            result2 = (string)taskType.GetProperty("Result")!.GetValue(task)!;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Unsafe: static field cross-contamination — both tasks read the same value
        Assert.Equal(result1, result2);
    }

    #endregion

    #region PartialMigration

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(typeof(FixedSubtle.PartialMigration))]
    public void PartialMigration_Fixed_BothPathAndEnvAreIsolated(Type taskType)
    {
        var barrier = new Barrier(2);
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var varName = $"SV_TEST_{Guid.NewGuid():N}";
        string? pathResult1 = null, envResult1 = null;
        string? pathResult2 = null, envResult2 = null;

        var t1 = new Thread(() =>
        {
            var env = new TaskEnvironment { ProjectDirectory = dir1 };
            env.SetEnvironmentVariable(varName, "env_value_1");
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            ((IMultiThreadableTask)task).TaskEnvironment = env;
            taskType.GetProperty("VariableName")!.SetValue(task, varName);
            taskType.GetProperty("InputPath")!.SetValue(task, "sub");
            barrier.SignalAndWait();
            task.Execute();
            pathResult1 = (string)taskType.GetProperty("PathResult")!.GetValue(task)!;
            envResult1 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
        });

        var t2 = new Thread(() =>
        {
            var env = new TaskEnvironment { ProjectDirectory = dir2 };
            env.SetEnvironmentVariable(varName, "env_value_2");
            var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
            task.BuildEngine = new MockBuildEngine();
            ((IMultiThreadableTask)task).TaskEnvironment = env;
            taskType.GetProperty("VariableName")!.SetValue(task, varName);
            taskType.GetProperty("InputPath")!.SetValue(task, "sub");
            barrier.SignalAndWait();
            task.Execute();
            pathResult2 = (string)taskType.GetProperty("PathResult")!.GetValue(task)!;
            envResult2 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Fixed: path resolves to own project dir
        Assert.StartsWith(dir1, pathResult1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, pathResult2!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(pathResult1, pathResult2);

        // Fixed: env variable reads from own TaskEnvironment
        Assert.Equal("env_value_1", envResult1);
        Assert.Equal("env_value_2", envResult2);
    }

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.PartialMigration))]
    public void PartialMigration_Unsafe_PathCorrectButEnvReadsFallbackGlobal(Type taskType)
    {
        var barrier = new Barrier(2);
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var varName = $"SV_TEST_{Guid.NewGuid():N}";
        string? pathResult1 = null, envResult1 = null;
        string? pathResult2 = null, envResult2 = null;

        // Don't set the process-global variable — the unsafe task will find nothing
        var originalValue = Environment.GetEnvironmentVariable(varName);

        try
        {
            var t1 = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir1 };
                env.SetEnvironmentVariable(varName, "env_value_1");
                var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new MockBuildEngine();
                ((IMultiThreadableTask)task).TaskEnvironment = env;
                taskType.GetProperty("VariableName")!.SetValue(task, varName);
                taskType.GetProperty("InputPath")!.SetValue(task, "sub");
                barrier.SignalAndWait();
                task.Execute();
                pathResult1 = (string)taskType.GetProperty("PathResult")!.GetValue(task)!;
                envResult1 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
            });

            var t2 = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir2 };
                env.SetEnvironmentVariable(varName, "env_value_2");
                var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new MockBuildEngine();
                ((IMultiThreadableTask)task).TaskEnvironment = env;
                taskType.GetProperty("VariableName")!.SetValue(task, varName);
                taskType.GetProperty("InputPath")!.SetValue(task, "sub");
                barrier.SignalAndWait();
                task.Execute();
                pathResult2 = (string)taskType.GetProperty("PathResult")!.GetValue(task)!;
                envResult2 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            // Path resolution IS correct (uses TaskEnvironment)
            Assert.StartsWith(dir1, pathResult1!, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, pathResult2!, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(pathResult1, pathResult2);

            // Env variable read is WRONG — reads from process-global Environment
            // which doesn't have the variable, so both return empty string
            Assert.Equal(string.Empty, envResult1);
            Assert.Equal(string.Empty, envResult2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion
}
