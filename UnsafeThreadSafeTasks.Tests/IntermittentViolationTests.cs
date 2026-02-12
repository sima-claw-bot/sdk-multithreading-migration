using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

using UnsafeIntermittent = UnsafeThreadSafeTasks.IntermittentViolations;

namespace UnsafeThreadSafeTasks.Tests;

public class IntermittentViolationTests : IDisposable
{
    private const int ThreadCount = 32;
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public IntermittentViolationTests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ivtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalCwd;
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    #region RegistryStyleGlobalState

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void RegistryStyleGlobalState_Unsafe_ConcurrentWritesCauseOverwrite(int iteration)
    {
        _ = iteration;
        var key = "shared_key";
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<string>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var value = $"value_{i}";
            var t = new Thread(() =>
            {
                var task = new UnsafeIntermittent.RegistryStyleGlobalState
                {
                    Key = key,
                    Value = value,
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                results.Add(task.Result);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // With a shared static dictionary and same key, concurrent tasks overwrite
        // each other. Most threads should read back a value written by another thread.
        var distinctValues = results.Distinct().ToList();
        // All threads wrote different values but read back from the same key after Sleep.
        // Due to the race, they should mostly read the same last-written value.
        Assert.True(distinctValues.Count < ThreadCount,
            $"Expected fewer distinct results than {ThreadCount} threads due to overwrites, got {distinctValues.Count}");
    }

    #endregion

    #region SharedTempFileConflict

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void SharedTempFileConflict_Unsafe_ConcurrentWritesClobberFile(int iteration)
    {
        _ = iteration;
        var barrier = new Barrier(ThreadCount);
        var mismatches = new ConcurrentBag<bool>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var content = $"content_from_thread_{i}";
            var t = new Thread(() =>
            {
                var task = new UnsafeIntermittent.SharedTempFileConflict
                {
                    Content = content,
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                try
                {
                    task.Execute();
                    mismatches.Add(task.ReadBack != content);
                }
                catch
                {
                    // IO exceptions from concurrent file access also demonstrate the bug
                    mismatches.Add(true);
                }
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // At least one thread should read back content written by a different thread
        Assert.Contains(true, mismatches);
    }

    #endregion

    #region EnvVarToctou

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void EnvVarToctou_Unsafe_ConcurrentModificationCausesMismatch(int iteration)
    {
        _ = iteration;
        var varName = $"TOCTOU_TEST_{Guid.NewGuid():N}";
        int readerCount = ThreadCount / 2;
        int writerCount = ThreadCount / 2;
        var barrier = new Barrier(readerCount + writerCount);
        var toctouDetected = new ConcurrentBag<bool>();
        var stop = new ManualResetEventSlim(false);
        var threads = new List<Thread>();

        try
        {
            Environment.SetEnvironmentVariable(varName, "initial");

            // Writer threads: continuously flip the env var during the task's 50ms sleep
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

            // Reader threads: run the TOCTOU task that reads, sleeps 50ms, reads again
            for (int i = 0; i < readerCount; i++)
            {
                var t = new Thread(() =>
                {
                    var task = new UnsafeIntermittent.EnvVarToctou
                    {
                        VariableName = varName,
                        BuildEngine = new MockBuildEngine()
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

            // At least one reader task should observe the env var changing between reads
            Assert.Contains(true, toctouDetected);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    #endregion

    #region StaticCachePathCollision

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void StaticCachePathCollision_Unsafe_SameRelativePathCollides(int iteration)
    {
        _ = iteration;
        // Use a unique relative path per iteration so the static cache starts empty for it,
        // then pre-seed it with one project directory. Subsequent concurrent tasks using
        // different directories will get the cached (wrong) result.
        var relativePath = $"src\\Program_{iteration}.cs";
        var seedDir = CreateTempDir();
        var expectedSeedPath = Path.GetFullPath(Path.Combine(seedDir, relativePath));

        // Pre-seed the static cache by running one task synchronously
        var seedTask = new UnsafeIntermittent.StaticCachePathCollision
        {
            ProjectDirectory = seedDir,
            RelativePath = relativePath,
            BuildEngine = new MockBuildEngine()
        };
        seedTask.Execute();

        // Now run concurrent tasks with DIFFERENT project directories
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<(string ProjectDir, string ResolvedPath)>();
        var threads = new List<Thread>();

        for (int i = 0; i < ThreadCount; i++)
        {
            var dir = CreateTempDir();
            var t = new Thread(() =>
            {
                var task = new UnsafeIntermittent.StaticCachePathCollision
                {
                    ProjectDirectory = dir,
                    RelativePath = relativePath,
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                try
                {
                    task.Execute();
                    results.Add((dir, task.ResolvedPath));
                }
                catch
                {
                    // Dictionary corruption from concurrent writes also proves the bug
                    results.Add((dir, "EXCEPTION"));
                }
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // All concurrent tasks used different directories but same relative path.
        // The cache returns the seed directory's result for all of them.
        var wrongResults = results.Where(r =>
            r.ResolvedPath != "EXCEPTION" &&
            r.ResolvedPath == expectedSeedPath).ToList();
        Assert.True(wrongResults.Count > 0,
            "Expected at least one task to receive the cached (wrong) path from the seed directory");
    }

    #endregion

    #region CwdRaceCondition

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void CwdRaceCondition_Unsafe_ConcurrentCwdChangesCorruptPaths(int iteration)
    {
        _ = iteration;
        var relativePath = "subdir\\file.txt";
        var barrier = new Barrier(ThreadCount);
        var mismatches = new ConcurrentBag<bool>();
        var threads = new List<Thread>();
        var dirs = new List<string>();

        for (int i = 0; i < ThreadCount; i++)
        {
            dirs.Add(CreateTempDir());
        }

        for (int i = 0; i < ThreadCount; i++)
        {
            var myDir = dirs[i];
            var expected = Path.GetFullPath(Path.Combine(myDir, relativePath));
            var t = new Thread(() =>
            {
                var task = new UnsafeIntermittent.CwdRaceCondition
                {
                    ProjectDirectory = myDir,
                    RelativePath = relativePath,
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                mismatches.Add(task.ResolvedPath != expected);
            });
            threads.Add(t);
        }

        foreach (var t in threads) t.Start();
        foreach (var t in threads) t.Join();

        // At least one thread should resolve against a CWD set by another thread
        Assert.Contains(true, mismatches);
    }

    #endregion

    #region LazyEnvVarCapture

    [Theory]
    [Trait("Category", "IntermittentViolation")]
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
    public void LazyEnvVarCapture_Unsafe_StaticLazyCapturesFirstValueOnly(int iteration)
    {
        _ = iteration;
        var varName = "MY_SETTING";
        var originalValue = Environment.GetEnvironmentVariable(varName);
        var barrier = new Barrier(ThreadCount);
        var results = new ConcurrentBag<string>();
        var threads = new List<Thread>();

        try
        {
            // Set an initial value; the Lazy<T> may already be initialized from
            // a prior test run in this process, so it will remain frozen.
            Environment.SetEnvironmentVariable(varName, "initial_value");

            for (int i = 0; i < ThreadCount; i++)
            {
                var myValue = $"thread_val_{i}";
                var t = new Thread(() =>
                {
                    // Each thread sets a different value before executing
                    Environment.SetEnvironmentVariable(varName, myValue);
                    var task = new UnsafeIntermittent.LazyEnvVarCapture
                    {
                        BuildEngine = new MockBuildEngine()
                    };
                    barrier.SignalAndWait();
                    task.Execute();
                    results.Add(task.Result);
                });
                threads.Add(t);
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            // The static Lazy captures once: all results should be the same frozen value
            var distinctResults = results.Distinct().ToList();
            Assert.Single(distinctResults);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion
}
