using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

    #region SharedMutableStaticField — basic execution and structural tests

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_ExecuteReturnsTrueAndSetsResult()
    {
        var task = new UnsafeSubtle.SharedMutableStaticField
        {
            InputValue = "test_value",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_SingleThreadReturnsInputValue()
    {
        var task = new UnsafeSubtle.SharedMutableStaticField
        {
            InputValue = "single_thread_value",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // In single-threaded mode, static field is not overwritten by another thread
        Assert.Equal("single_thread_value", task.Result);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.SharedMutableStaticField)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeSubtle.SharedMutableStaticField)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_InheritsFromMSBuildTask()
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(typeof(UnsafeSubtle.SharedMutableStaticField)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void SharedMutableStaticField_Unsafe_HasRequiredProperties()
    {
        var inputProp = typeof(UnsafeSubtle.SharedMutableStaticField).GetProperty("InputValue");
        var resultProp = typeof(UnsafeSubtle.SharedMutableStaticField).GetProperty("Result");

        Assert.NotNull(inputProp);
        Assert.NotNull(resultProp);
        Assert.NotNull(inputProp!.GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(resultProp!.GetCustomAttribute<OutputAttribute>());
    }

    #endregion

    #region PartialMigration — basic execution and structural tests

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void PartialMigration_Unsafe_ExecuteReturnsTrueAndSetsOutputs()
    {
        var projDir = CreateTempDir();
        var varName = $"SV_BASIC_{Guid.NewGuid():N}";
        var originalVal = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "global_val");

            var task = new UnsafeSubtle.PartialMigration
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.PathResult);
            Assert.NotEmpty(task.EnvResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalVal);
        }
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void PartialMigration_Unsafe_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.PartialMigration)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void PartialMigration_Unsafe_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeSubtle.PartialMigration)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void PartialMigration_Unsafe_PathResolutionUsesTaskEnvironment()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeSubtle.PartialMigration
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            VariableName = "NONEXISTENT_VAR",
            InputPath = "sub",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Path resolution correctly uses TaskEnvironment
        Assert.StartsWith(projDir, task.PathResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void PartialMigration_Unsafe_EnvVarReadsFromProcessGlobal_NotTaskEnvironment()
    {
        var varName = $"SV_ENV_{Guid.NewGuid():N}";
        var originalVal = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "process_global_value");

            var env = new TaskEnvironment();
            env.SetEnvironmentVariable(varName, "task_env_value");

            var task = new UnsafeSubtle.PartialMigration
            {
                TaskEnvironment = env,
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = new MockBuildEngine()
            };

            task.Execute();

            // BUG: reads from process global, not TaskEnvironment
            Assert.Equal("process_global_value", task.EnvResult);
            Assert.NotEqual("task_env_value", task.EnvResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalVal);
        }
    }

    #endregion

    #region DoubleResolvesPath — structural tests

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.DoubleResolvesPath)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeSubtle.DoubleResolvesPath)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_ExecuteReturnsTrueWithRelativePath()
    {
        var task = new UnsafeSubtle.DoubleResolvesPath
        {
            InputPath = "relative\\path\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.True(Path.IsPathRooted(task.Result));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void DoubleResolvesPath_Unsafe_DoubleGetFullPathProducesSameResultAsSingle()
    {
        var relativePath = Path.Combine("some", "relative", "path.txt");
        var task = new UnsafeSubtle.DoubleResolvesPath
        {
            InputPath = relativePath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Double Path.GetFullPath is redundant — same as single
        Assert.Equal(Path.GetFullPath(relativePath), task.Result);
    }

    #endregion

    #region IndirectPathGetFullPath — structural tests

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void IndirectPathGetFullPath_Unsafe_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.IndirectPathGetFullPath)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void IndirectPathGetFullPath_Unsafe_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeSubtle.IndirectPathGetFullPath)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void IndirectPathGetFullPath_Unsafe_ExecuteReturnsTrueAndResolvesPath()
    {
        var task = new UnsafeSubtle.IndirectPathGetFullPath
        {
            InputPath = "relative\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.True(Path.IsPathRooted(task.Result));
        Assert.Equal(Path.GetFullPath("relative\\file.txt"), task.Result);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void IndirectPathGetFullPath_Unsafe_AbsolutePathPassesThroughUnchanged()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeSubtle.IndirectPathGetFullPath
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
            InputPath = absPath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(absPath, task.Result);
    }

    #endregion

    #region LambdaCapturesCurrentDirectory — structural tests

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.LambdaCapturesCurrentDirectory)));
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeSubtle.LambdaCapturesCurrentDirectory)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_EmptyInputFilesReturnsEmptyArray()
    {
        var task = new UnsafeSubtle.LambdaCapturesCurrentDirectory
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
            InputFiles = Array.Empty<ITaskItem>(),
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    public void LambdaCapturesCurrentDirectory_Unsafe_MultipleFilesAllResolveAgainstCwd()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeSubtle.LambdaCapturesCurrentDirectory
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = new ITaskItem[]
            {
                new Microsoft.Build.Utilities.TaskItem("a.txt"),
                new Microsoft.Build.Utilities.TaskItem("b.txt"),
                new Microsoft.Build.Utilities.TaskItem("sub\\c.txt")
            },
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(3, task.ResolvedPaths.Length);
        foreach (var path in task.ResolvedPaths)
        {
            // BUG: all resolve against Environment.CurrentDirectory, not ProjectDirectory
            Assert.StartsWith(Environment.CurrentDirectory, path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projDir, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region All unsafe SubtleViolation tasks — InheritsFromMSBuildTask

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.SharedMutableStaticField))]
    [InlineData(typeof(UnsafeSubtle.PartialMigration))]
    [InlineData(typeof(UnsafeSubtle.DoubleResolvesPath))]
    [InlineData(typeof(UnsafeSubtle.IndirectPathGetFullPath))]
    [InlineData(typeof(UnsafeSubtle.LambdaCapturesCurrentDirectory))]
    public void AllUnsafeSubtleViolations_InheritFromMSBuildTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "SubtleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeSubtle.SharedMutableStaticField))]
    [InlineData(typeof(UnsafeSubtle.PartialMigration))]
    [InlineData(typeof(UnsafeSubtle.DoubleResolvesPath))]
    [InlineData(typeof(UnsafeSubtle.IndirectPathGetFullPath))]
    [InlineData(typeof(UnsafeSubtle.LambdaCapturesCurrentDirectory))]
    public void AllUnsafeSubtleViolations_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
        Assert.IsAssignableFrom<MSBuildTask>(instance);
    }

    #endregion
}
