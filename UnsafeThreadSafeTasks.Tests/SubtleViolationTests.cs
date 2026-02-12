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

    #region DoubleResolvesPath

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.DoubleResolvesPath))]
    public void DoubleResolvesPath_Unsafe_BothResolveAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: both resolve against CWD, so both produce the same result
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_IgnoresTaskEnvironment()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeSubtle.DoubleResolvesPath
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "sub\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // BUG: resolves against CWD, not ProjectDirectory
        Assert.DoesNotContain(projDir, task.Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath("sub\\file.txt"), task.Result);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_AbsoluteInputRedundantOuterCall()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeSubtle.DoubleResolvesPath
        {
            InputPath = absPath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Outer GetFullPath is redundant when inner already returns absolute
        Assert.Equal(absPath, task.Result);
    }

    #endregion

    #region IndirectPathGetFullPath

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.IndirectPathGetFullPath))]
    public void IndirectPathGetFullPath_Unsafe_BothResolveAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Unsafe: private helper still uses Path.GetFullPath — both resolve against CWD
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void IndirectPathGetFullPath_Unsafe_IgnoresTaskEnvironment()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeSubtle.IndirectPathGetFullPath
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "sub\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // BUG: private ResolvePath uses Path.GetFullPath which resolves against CWD
        Assert.DoesNotContain(projDir, task.Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath("sub\\file.txt"), task.Result);
    }

    #endregion

    #region LambdaCapturesCurrentDirectory

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_UsesProcessGlobalCwd()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeSubtle.LambdaCapturesCurrentDirectory
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = new ITaskItem[]
            {
                new Microsoft.Build.Utilities.TaskItem("file1.txt"),
                new Microsoft.Build.Utilities.TaskItem("file2.txt")
            },
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(2, task.ResolvedPaths.Length);
        // BUG: paths combined with Environment.CurrentDirectory, not ProjectDirectory
        foreach (var path in task.ResolvedPaths)
        {
            Assert.StartsWith(Environment.CurrentDirectory, path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projDir, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_ConcurrentBothUseProcessCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string[]? resolved1 = null, resolved2 = null;
        Exception? ex1 = null, ex2 = null;

        var t1 = new Thread(() =>
        {
            try
            {
                var task = new UnsafeSubtle.LambdaCapturesCurrentDirectory
                {
                    TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                    InputFiles = new ITaskItem[] { new Microsoft.Build.Utilities.TaskItem("a.txt") },
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                resolved1 = task.ResolvedPaths;
            }
            catch (Exception ex) { ex1 = ex; }
        });

        var t2 = new Thread(() =>
        {
            try
            {
                var task = new UnsafeSubtle.LambdaCapturesCurrentDirectory
                {
                    TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                    InputFiles = new ITaskItem[] { new Microsoft.Build.Utilities.TaskItem("a.txt") },
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                resolved2 = task.ResolvedPaths;
            }
            catch (Exception ex) { ex2 = ex; }
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        if (ex1 != null) throw new AggregateException("Task 1 failed", ex1);
        if (ex2 != null) throw new AggregateException("Task 2 failed", ex2);

        // Unsafe: both tasks use the same process-global Environment.CurrentDirectory
        Assert.Single(resolved1!);
        Assert.Single(resolved2!);
        Assert.Equal(resolved1![0], resolved2![0]);
    }

    #endregion
}
