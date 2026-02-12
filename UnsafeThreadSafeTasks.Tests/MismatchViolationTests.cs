#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

using UnsafePath = UnsafeThreadSafeTasks.PathViolations;
using UnsafeSubtle = UnsafeThreadSafeTasks.SubtleViolations;
using UnsafeProcess = UnsafeThreadSafeTasks.ProcessViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Black-box concurrent tests for tasks that claim thread-safety
/// (implement IMultiThreadableTask) but still produce incorrect results
/// when run with non-CWD project directories.
/// </summary>
public class MismatchViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempProjectDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mvtest_{Guid.NewGuid():N}");
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

    #region UsesPathGetFullPath_IgnoresTaskEnv — claims IMultiThreadableTask but uses Path.GetFullPath

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafePath.UsesPathGetFullPath_IgnoresTaskEnv))]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ClaimsThreadSafe_ButResolvesAgainstCwd(Type taskType)
    {
        // Task implements IMultiThreadableTask, so it claims to be thread-safe
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Mismatch: despite claiming thread-safety, both resolve against CWD
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafePath.UsesPathGetFullPath_IgnoresTaskEnv))]
    public void UsesPathGetFullPath_IgnoresTaskEnv_ClaimsThreadSafe_ButIgnoresProjectDirectory(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "file.txt";

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Both tasks should resolve to different paths if truly thread-safe
        // but they both resolve against CWD, producing the same result
        Assert.Equal(result1, result2);

        // The results should contain the project dir if correctly resolved
        var expectedPath1 = Path.GetFullPath(Path.Combine(dir1, relativePath));
        var expectedPath2 = Path.GetFullPath(Path.Combine(dir2, relativePath));
        Assert.NotEqual(expectedPath1, result1);
        Assert.NotEqual(expectedPath2, result2);
    }

    #endregion

    #region DoubleResolvesPath — claims IMultiThreadableTask but double-resolves against CWD

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.DoubleResolvesPath))]
    public void DoubleResolvesPath_ClaimsThreadSafe_ButBothResolveAgainstCwd(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Mismatch: implements IMultiThreadableTask but both resolve against CWD
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.DoubleResolvesPath))]
    public void DoubleResolvesPath_ClaimsThreadSafe_ButResultIsProcessCwdBased(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "test.txt";

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Result is CWD-based, not project-dir-based
        var cwdBased = Path.GetFullPath(relativePath);
        Assert.Equal(cwdBased, result1);
        Assert.Equal(cwdBased, result2);
    }

    #endregion

    #region IndirectPathGetFullPath — claims IMultiThreadableTask but private helper uses Path.GetFullPath

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.IndirectPathGetFullPath))]
    public void IndirectPathGetFullPath_ClaimsThreadSafe_ButBothResolveAgainstCwd(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("subdir", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // Mismatch: private helper still uses Path.GetFullPath — both resolve against CWD
        Assert.Equal(result1, result2);
        Assert.DoesNotContain(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.IndirectPathGetFullPath))]
    public void IndirectPathGetFullPath_ClaimsThreadSafe_ButResultIsProcessCwdBased(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = "data.txt";

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        var cwdBased = Path.GetFullPath(relativePath);
        Assert.Equal(cwdBased, result1);
        Assert.Equal(cwdBased, result2);
    }

    #endregion

    #region LambdaCapturesCurrentDirectory — claims IMultiThreadableTask but uses Environment.CurrentDirectory

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.LambdaCapturesCurrentDirectory))]
    public void LambdaCapturesCurrentDirectory_ClaimsThreadSafe_ButBothUseProcessCwd(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();

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
                    InputFiles = new ITaskItem[] { new TaskItem("file.txt") },
                    BuildEngine = new TestBuildEngine()
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
                    InputFiles = new ITaskItem[] { new TaskItem("file.txt") },
                    BuildEngine = new TestBuildEngine()
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

        // Mismatch: despite claiming thread-safety, both tasks use process-global CWD
        Assert.Single(resolved1!);
        Assert.Single(resolved2!);
        Assert.Equal(resolved1![0], resolved2![0]);
        Assert.DoesNotContain(dir1, resolved1[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(dir2, resolved2[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region PartialMigration — claims [MSBuildMultiThreadableTask] but env reads fall back to global

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.PartialMigration))]
    public void PartialMigration_ClaimsThreadSafe_ButEnvReadsFallBackToGlobal(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
        Assert.NotNull(Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute)));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        var varName = $"MV_TEST_{Guid.NewGuid():N}";
        string? pathResult1 = null, envResult1 = null;
        string? pathResult2 = null, envResult2 = null;

        var originalValue = Environment.GetEnvironmentVariable(varName);
        try
        {
            var barrier = new Barrier(2);

            var t1 = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir1 };
                env.SetEnvironmentVariable(varName, "env_value_1");
                var task = (Task)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new TestBuildEngine();
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
                var task = (Task)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new TestBuildEngine();
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

            // Path resolution is correct (uses TaskEnvironment)
            Assert.StartsWith(dir1, pathResult1!, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(dir2, pathResult2!, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(pathResult1, pathResult2);

            // Mismatch: despite [MSBuildMultiThreadableTask], env variable reads
            // fall back to process-global Environment which doesn't have the variable
            Assert.Equal(string.Empty, envResult1);
            Assert.Equal(string.Empty, envResult2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeSubtle.PartialMigration))]
    public void PartialMigration_ClaimsThreadSafe_ButEnvReadsFromProcessGlobal(Type taskType)
    {
        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        var varName = $"MV_GLOBAL_{Guid.NewGuid():N}";

        var originalValue = Environment.GetEnvironmentVariable(varName);
        try
        {
            // Set process-global value — task should NOT read this
            Environment.SetEnvironmentVariable(varName, "global_leaked_value");

            var barrier = new Barrier(2);
            string? envResult1 = null, envResult2 = null;

            var t1 = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir1 };
                env.SetEnvironmentVariable(varName, "isolated_1");
                var task = (Task)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new TestBuildEngine();
                ((IMultiThreadableTask)task).TaskEnvironment = env;
                taskType.GetProperty("VariableName")!.SetValue(task, varName);
                taskType.GetProperty("InputPath")!.SetValue(task, "sub");
                barrier.SignalAndWait();
                task.Execute();
                envResult1 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
            });

            var t2 = new Thread(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir2 };
                env.SetEnvironmentVariable(varName, "isolated_2");
                var task = (Task)Activator.CreateInstance(taskType)!;
                task.BuildEngine = new TestBuildEngine();
                ((IMultiThreadableTask)task).TaskEnvironment = env;
                taskType.GetProperty("VariableName")!.SetValue(task, varName);
                taskType.GetProperty("InputPath")!.SetValue(task, "sub");
                barrier.SignalAndWait();
                task.Execute();
                envResult2 = (string)taskType.GetProperty("EnvResult")!.GetValue(task)!;
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            // Mismatch: both tasks read the process-global value, not the isolated one
            Assert.Equal("global_leaked_value", envResult1);
            Assert.Equal("global_leaked_value", envResult2);
            Assert.NotEqual("isolated_1", envResult1);
            Assert.NotEqual("isolated_2", envResult2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion

    #region UsesRawProcessStartInfo — claims IMultiThreadableTask but spawns process with wrong CWD

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [InlineData(typeof(UnsafeProcess.UsesRawProcessStartInfo))]
    public void UsesRawProcessStartInfo_ClaimsThreadSafe_ButAllProcessesUseProcessCwd(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        var processCwd = Directory.GetCurrentDirectory();

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        Exception? ex1 = null, ex2 = null;

        var t1 = new Thread(() =>
        {
            try
            {
                var task = new UnsafeProcess.UsesRawProcessStartInfo
                {
                    TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                    Command = "cmd.exe",
                    Arguments = "/c cd",
                    BuildEngine = new TestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                result1 = task.Result;
            }
            catch (Exception ex) { ex1 = ex; }
        });

        var t2 = new Thread(() =>
        {
            try
            {
                var task = new UnsafeProcess.UsesRawProcessStartInfo
                {
                    TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                    Command = "cmd.exe",
                    Arguments = "/c cd",
                    BuildEngine = new TestBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                result2 = task.Result;
            }
            catch (Exception ex) { ex2 = ex; }
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        if (ex1 != null) throw new AggregateException("Task 1 failed", ex1);
        if (ex2 != null) throw new AggregateException("Task 2 failed", ex2);

        // Mismatch: despite IMultiThreadableTask, all processes use process CWD
        Assert.Equal(processCwd, result1);
        Assert.Equal(processCwd, result2);
        Assert.NotEqual(dir1, result1);
        Assert.NotEqual(dir2, result2);
    }

    #endregion

    #region Cross-cutting: all mismatch tasks implement IMultiThreadableTask but produce wrong results

    public static TheoryData<Type, string> AllMismatchTaskTypes => new()
    {
        { typeof(UnsafePath.UsesPathGetFullPath_IgnoresTaskEnv), "PathViolations.UsesPathGetFullPath_IgnoresTaskEnv" },
        { typeof(UnsafeSubtle.DoubleResolvesPath), "SubtleViolations.DoubleResolvesPath" },
        { typeof(UnsafeSubtle.IndirectPathGetFullPath), "SubtleViolations.IndirectPathGetFullPath" },
    };

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [MemberData(nameof(AllMismatchTaskTypes))]
    public void MismatchTask_ImplementsIMultiThreadableTask_ButProducesSameResultForDifferentDirs(
        Type taskType, string _)
    {
        // Verify the task claims thread-safety
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));

        var dir1 = CreateTempProjectDir();
        var dir2 = CreateTempProjectDir();
        string relativePath = Path.Combine("a", "b", "file.txt");

        var (result1, result2) = TestHelper.RunTaskConcurrently(taskType, dir1, dir2, relativePath);

        // A correctly thread-safe task would produce different results for different project dirs
        // but these mismatch tasks produce the same CWD-based result
        Assert.Equal(result1, result2);
    }

    #endregion
}
