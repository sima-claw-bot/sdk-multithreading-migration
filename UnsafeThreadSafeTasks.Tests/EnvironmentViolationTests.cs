#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xunit;

using FixedEnv = FixedThreadSafeTasks.EnvironmentViolations;
using UnsafeEnv = UnsafeThreadSafeTasks.EnvironmentViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class EnvironmentViolationTests : IDisposable
{
    private readonly List<string> _tempDirs = new();
    private readonly string _originalCwd;

    public EnvironmentViolationTests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    public void Dispose()
    {
        // Restore original state
        Environment.CurrentDirectory = _originalCwd;

        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"envtest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    #region ReadsEnvironmentCurrentDirectory

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void ReadsEnvironmentCurrentDirectory_Execute_ReturnsCurrentDirectory()
    {
        var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Environment.CurrentDirectory, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void ReadsEnvironmentCurrentDirectory_Execute_ReturnsProcessGlobalCwd()
    {
        var tempDir = CreateTempDir();
        // Demonstrates the bug: the task reads process-global state
        Environment.CurrentDirectory = tempDir;

        var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(tempDir, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public async Task ReadsEnvironmentCurrentDirectory_ConcurrentExecution_AllReadSameGlobalState()
    {
        // Both tasks see the same process-global CWD regardless of intended project directory
        var tempDir = CreateTempDir();
        var dir1 = Path.Combine(tempDir, "proj1");
        var dir2 = Path.Combine(tempDir, "proj2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            // Imagine this task should read dir1 as its project directory
            barrier.SignalAndWait();
            var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            // Imagine this task should read dir2 as its project directory
            barrier.SignalAndWait();
            var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Unsafe: both tasks read the same process-global CWD
        Assert.Equal(result1, result2);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void ReadsCurrentDirectory_Fixed_EachTaskReadsOwnProjectDirectory()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var t1 = new Thread(() =>
        {
            var task = new FixedEnv.ReadsEnvironmentCurrentDirectory
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedEnv.ReadsEnvironmentCurrentDirectory
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.Equal(dir1, result1);
        Assert.Equal(dir2, result2);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Unsafe")]
    public void ReadsCurrentDirectory_Unsafe_ReadsProcessGlobalCwd()
    {
        var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory
        {
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Unsafe: reads from process-global Environment.CurrentDirectory
        Assert.Equal(Environment.CurrentDirectory, task.Result);
    }

    #endregion

    #region SetsEnvironmentCurrentDirectory

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_Execute_ChangesGlobalCwdAndReadsFile()
    {
        var tempDir = CreateTempDir();
        var filePath = "testfile.txt";
        File.WriteAllText(Path.Combine(tempDir, filePath), "hello from temp");

        var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
        {
            NewDirectory = tempDir,
            RelativeFilePath = filePath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello from temp", task.Result);
        // Side effect: process-global CWD is now changed
        Assert.Equal(tempDir, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_Execute_MutatesProcessGlobalState()
    {
        var originalCwd = Environment.CurrentDirectory;
        var dir = CreateTempDir();
        var subdir = Path.Combine(dir, "subdir");
        Directory.CreateDirectory(subdir);
        File.WriteAllText(Path.Combine(subdir, "data.txt"), "content");

        var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
        {
            NewDirectory = subdir,
            RelativeFilePath = "data.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Demonstrates the bug: CWD has been mutated for the entire process
        Assert.NotEqual(originalCwd, Environment.CurrentDirectory);
        Assert.Equal(subdir, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_WithNonExistentFile_Throws()
    {
        var tempDir = CreateTempDir();
        var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
        {
            NewDirectory = tempDir,
            RelativeFilePath = "nonexistent.txt",
            BuildEngine = new MockBuildEngine()
        };

        Assert.Throws<FileNotFoundException>(() => task.Execute());
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public async Task SetsEnvironmentCurrentDirectory_ConcurrentExecution_RaceConditionOnGlobalCwd()
    {
        // Two tasks both set the process-global CWD and read a relative file.
        // Because CWD is shared, one task may read the other's file.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = "data.txt";
        File.WriteAllText(Path.Combine(dir1, relativePath), "from_dir1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "from_dir2");

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
            {
                NewDirectory = dir1,
                RelativeFilePath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
            {
                NewDirectory = dir2,
                RelativeFilePath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Each result must be one of the two valid file contents, but because CWD
        // is process-global the actual values are non-deterministic.
        Assert.True(
            result1 == "from_dir1" || result1 == "from_dir2",
            $"result1 should be from one of the directories, got: {result1}");
        Assert.True(
            result2 == "from_dir1" || result2 == "from_dir2",
            $"result2 should be from one of the directories, got: {result2}");
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Unsafe")]
    public void SetsCurrentDirectory_Unsafe_MutatesProcessGlobalCwd()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "file.txt"), "content");

        var task = new UnsafeEnv.SetsEnvironmentCurrentDirectory
        {
            NewDirectory = dir,
            RelativeFilePath = "file.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Unsafe: process-global CWD was mutated as a side effect
        Assert.Equal(dir, Environment.CurrentDirectory);
        Assert.Equal("content", task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void SetsCurrentDirectory_Fixed_EachTaskUsesOwnProjectDirectory()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = "testfile.txt";

        File.WriteAllText(Path.Combine(dir1, relativePath), "content_dir1");
        File.WriteAllText(Path.Combine(dir2, relativePath), "content_dir2");

        var t1 = new Thread(() =>
        {
            var task = new FixedEnv.SetsEnvironmentCurrentDirectory
            {
                TaskEnvironment = new TaskEnvironment(),
                NewDirectory = dir1,
                RelativeFilePath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedEnv.SetsEnvironmentCurrentDirectory
            {
                TaskEnvironment = new TaskEnvironment(),
                NewDirectory = dir2,
                RelativeFilePath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.Equal("content_dir1", result1);
        Assert.Equal("content_dir2", result2);
    }

    #endregion

    #region UsesEnvironmentGetVariable

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentGetVariable_Execute_ReadsProcessEnvironmentVariable()
    {
        var varName = $"TEST_ENV_GET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, "test_value");
        try
        {
            var task = new UnsafeEnv.UsesEnvironmentGetVariable
            {
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("test_value", task.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentGetVariable_WithUnsetVariable_ReturnsEmptyString()
    {
        var task = new UnsafeEnv.UsesEnvironmentGetVariable
        {
            VariableName = $"NONEXISTENT_{Guid.NewGuid():N}",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public async Task UsesEnvironmentGetVariable_ConcurrentExecution_ReadsProcessGlobalState()
    {
        var varName = $"TEST_CONCURRENT_GET_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, "shared_value");
        try
        {
            var barrier = new Barrier(2);
            string? result1 = null, result2 = null;

            var t1 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var task = new UnsafeEnv.UsesEnvironmentGetVariable
                {
                    VariableName = varName,
                    BuildEngine = new MockBuildEngine()
                };
                task.Execute();
                result1 = task.Result;
            });

            var t2 = Task.Run(() =>
            {
                barrier.SignalAndWait();
                var task = new UnsafeEnv.UsesEnvironmentGetVariable
                {
                    VariableName = varName,
                    BuildEngine = new MockBuildEngine()
                };
                task.Execute();
                result2 = task.Result;
            });

            await Task.WhenAll(t1, t2);

            // Both tasks read the same process-global environment variable
            Assert.Equal("shared_value", result1);
            Assert.Equal("shared_value", result2);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void GetVariable_Fixed_EachTaskReadsFromOwnEnvironment()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        var varName = $"TEST_VAR_{Guid.NewGuid():N}";

        var env1 = new TaskEnvironment();
        env1.SetEnvironmentVariable(varName, "value_from_env1");
        var env2 = new TaskEnvironment();
        env2.SetEnvironmentVariable(varName, "value_from_env2");

        var t1 = new Thread(() =>
        {
            var task = new FixedEnv.UsesEnvironmentGetVariable
            {
                TaskEnvironment = env1,
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedEnv.UsesEnvironmentGetVariable
            {
                TaskEnvironment = env2,
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.Equal("value_from_env1", result1);
        Assert.Equal("value_from_env2", result2);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Unsafe")]
    public void GetVariable_Unsafe_ReadsFromProcessGlobalState()
    {
        var varName = $"TEST_VAR_{Guid.NewGuid():N}";
        var originalValue = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "global_value");
            var task = new UnsafeEnv.UsesEnvironmentGetVariable
            {
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };

            task.Execute();

            Assert.Equal("global_value", task.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion

    #region UsesEnvironmentSetVariable

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentSetVariable_Execute_SetsAndReadsProcessEnvironmentVariable()
    {
        var varName = $"TEST_ENV_SET_{Guid.NewGuid():N}";
        try
        {
            var task = new UnsafeEnv.UsesEnvironmentSetVariable
            {
                Name = varName,
                Value = "my_value",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("my_value", task.Result);
            // Side effect: process-global env var is now set
            Assert.Equal("my_value", Environment.GetEnvironmentVariable(varName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentSetVariable_Execute_MutatesProcessGlobalState()
    {
        var varName = $"TEST_MUTATE_{Guid.NewGuid():N}";
        try
        {
            Assert.Null(Environment.GetEnvironmentVariable(varName));

            var task = new UnsafeEnv.UsesEnvironmentSetVariable
            {
                Name = varName,
                Value = "polluted",
                BuildEngine = new MockBuildEngine()
            };
            task.Execute();

            // Demonstrates the bug: the env var is now visible to the entire process
            Assert.Equal("polluted", Environment.GetEnvironmentVariable(varName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public async Task UsesEnvironmentSetVariable_ConcurrentExecution_RaceConditionOnSharedState()
    {
        // Two tasks writing to the same process-global env var concurrently
        var varName = $"TEST_RACE_{Guid.NewGuid():N}";
        try
        {
            var barrier = new Barrier(2);
            string? result1 = null, result2 = null;

            var t1 = Task.Run(() =>
            {
                var task = new UnsafeEnv.UsesEnvironmentSetVariable
                {
                    Name = varName,
                    Value = "value_from_task1",
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                result1 = task.Result;
            });

            var t2 = Task.Run(() =>
            {
                var task = new UnsafeEnv.UsesEnvironmentSetVariable
                {
                    Name = varName,
                    Value = "value_from_task2",
                    BuildEngine = new MockBuildEngine()
                };
                barrier.SignalAndWait();
                task.Execute();
                result2 = task.Result;
            });

            await Task.WhenAll(t1, t2);

            // Each task reads back the process-global state which may have been overwritten
            // by the other task. The final value is non-deterministic but must be one of the two.
            var finalValue = Environment.GetEnvironmentVariable(varName);
            Assert.True(
                finalValue == "value_from_task1" || finalValue == "value_from_task2",
                $"Final env var value should be one of the two task values, got: {finalValue}");
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentSetVariable_OverwritesExistingVariable()
    {
        var varName = $"TEST_OVERWRITE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, "original");
        try
        {
            var task = new UnsafeEnv.UsesEnvironmentSetVariable
            {
                Name = varName,
                Value = "overwritten",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("overwritten", task.Result);
            Assert.Equal("overwritten", Environment.GetEnvironmentVariable(varName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void SetVariable_Fixed_EachTaskWritesToOwnEnvironment()
    {
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;
        var varName = $"TEST_SET_{Guid.NewGuid():N}";

        var env1 = new TaskEnvironment();
        var env2 = new TaskEnvironment();

        var t1 = new Thread(() =>
        {
            var task = new FixedEnv.UsesEnvironmentSetVariable
            {
                TaskEnvironment = env1,
                Name = varName,
                Value = "val_task1",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedEnv.UsesEnvironmentSetVariable
            {
                TaskEnvironment = env2,
                Name = varName,
                Value = "val_task2",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.Equal("val_task1", result1);
        Assert.Equal("val_task2", result2);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Unsafe")]
    public void SetVariable_Unsafe_WritesToProcessGlobalState()
    {
        var varName = $"TEST_SET_{Guid.NewGuid():N}";
        var originalValue = Environment.GetEnvironmentVariable(varName);

        try
        {
            var task = new UnsafeEnv.UsesEnvironmentSetVariable
            {
                Name = varName,
                Value = "written_by_task",
                BuildEngine = new MockBuildEngine()
            };

            task.Execute();

            // The unsafe task writes to process-global state
            Assert.Equal("written_by_task", Environment.GetEnvironmentVariable(varName));
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalValue);
        }
    }

    #endregion

    #region Edge cases

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void ReadsEnvironmentCurrentDirectory_ResultIsNonEmpty()
    {
        var task = new UnsafeEnv.ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };

        task.Execute();

        Assert.NotNull(task.Result);
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentGetVariable_WithEmptyStringValue_ReturnsEmptyString()
    {
        var varName = $"TEST_EMPTY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(varName, string.Empty);
        try
        {
            var task = new UnsafeEnv.UsesEnvironmentGetVariable
            {
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };

            task.Execute();

            // On Windows, setting empty string removes the variable
            // The result should be empty string either way
            Assert.Equal(string.Empty, task.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentSetVariable_ThenGetVariable_ReadsSameValue()
    {
        var varName = $"TEST_SET_GET_{Guid.NewGuid():N}";
        try
        {
            var setTask = new UnsafeEnv.UsesEnvironmentSetVariable
            {
                Name = varName,
                Value = "set_value",
                BuildEngine = new MockBuildEngine()
            };
            setTask.Execute();

            var getTask = new UnsafeEnv.UsesEnvironmentGetVariable
            {
                VariableName = varName,
                BuildEngine = new MockBuildEngine()
            };
            getTask.Execute();

            // Unsafe: both tasks share process-global state
            Assert.Equal("set_value", getTask.Result);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void FixedGetVariable_WithUnsetVariable_ReturnsEmptyString()
    {
        var env = new TaskEnvironment();
        var task = new FixedEnv.UsesEnvironmentGetVariable
        {
            TaskEnvironment = env,
            VariableName = "NONEXISTENT_VARIABLE",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void FixedSetThenGet_IsolatedFromProcessGlobal()
    {
        var varName = $"TEST_ISOLATED_{Guid.NewGuid():N}";
        var env = new TaskEnvironment();

        var setTask = new FixedEnv.UsesEnvironmentSetVariable
        {
            TaskEnvironment = env,
            Name = varName,
            Value = "isolated_value",
            BuildEngine = new MockBuildEngine()
        };
        setTask.Execute();

        // The process-global state should NOT be affected
        Assert.Null(Environment.GetEnvironmentVariable(varName));
        Assert.Equal("isolated_value", setTask.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void FixedReadsCurrentDirectory_DoesNotChangeProcessGlobalCwd()
    {
        var originalCwd = Environment.CurrentDirectory;
        var dir = CreateTempDir();

        var task = new FixedEnv.ReadsEnvironmentCurrentDirectory
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir },
            BuildEngine = new MockBuildEngine()
        };
        task.Execute();

        Assert.Equal(dir, task.Result);
        // The process-global CWD should be unchanged
        Assert.Equal(originalCwd, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void FixedSetsCurrentDirectory_DoesNotChangeProcessGlobalCwd()
    {
        var originalCwd = Environment.CurrentDirectory;
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "test.txt"), "data");

        var task = new FixedEnv.SetsEnvironmentCurrentDirectory
        {
            TaskEnvironment = new TaskEnvironment(),
            NewDirectory = dir,
            RelativeFilePath = "test.txt",
            BuildEngine = new MockBuildEngine()
        };
        task.Execute();

        Assert.Equal("data", task.Result);
        // The process-global CWD should be unchanged
        Assert.Equal(originalCwd, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    [Trait("Target", "Fixed")]
    public void FixedSetVariable_DoesNotMutateProcessGlobalState()
    {
        var varName = $"TEST_FIXED_NOMUTATE_{Guid.NewGuid():N}";
        try
        {
            var env = new TaskEnvironment();
            var task = new FixedEnv.UsesEnvironmentSetVariable
            {
                TaskEnvironment = env,
                Name = varName,
                Value = "fixed_value",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("fixed_value", task.Result);
            // The process-global state should NOT be affected
            Assert.Null(Environment.GetEnvironmentVariable(varName));
        }
        finally
        {
            // Clean up in case the fixed version unexpectedly wrote to process-global state
            Environment.SetEnvironmentVariable(varName, null);
        }
    }

    #endregion

    #region Interface and attribute verification

    public static IEnumerable<object[]> UnsafeEnvironmentTaskTypes()
    {
        yield return new object[] { typeof(UnsafeEnv.ReadsEnvironmentCurrentDirectory) };
        yield return new object[] { typeof(UnsafeEnv.SetsEnvironmentCurrentDirectory) };
        yield return new object[] { typeof(UnsafeEnv.UsesEnvironmentGetVariable) };
        yield return new object[] { typeof(UnsafeEnv.UsesEnvironmentSetVariable) };
    }

    public static IEnumerable<object[]> FixedEnvironmentTaskTypes()
    {
        yield return new object[] { typeof(FixedEnv.ReadsEnvironmentCurrentDirectory) };
        yield return new object[] { typeof(FixedEnv.SetsEnvironmentCurrentDirectory) };
        yield return new object[] { typeof(FixedEnv.UsesEnvironmentGetVariable) };
        yield return new object[] { typeof(FixedEnv.UsesEnvironmentSetVariable) };
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(UnsafeEnvironmentTaskTypes))]
    public void UnsafeEnvironmentTask_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        var task = Activator.CreateInstance(taskType)!;
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(UnsafeEnvironmentTaskTypes))]
    public void UnsafeEnvironmentTask_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(FixedEnvironmentTaskTypes))]
    public void FixedEnvironmentTask_ImplementsIMultiThreadableTask(Type taskType)
    {
        var task = Activator.CreateInstance(taskType)!;
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(FixedEnvironmentTaskTypes))]
    public void FixedEnvironmentTask_HasMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.NotNull(attr);
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(FixedEnvironmentTaskTypes))]
    public void FixedEnvironmentTask_IsAssignableFromMSBuildTask(Type taskType)
    {
        var task = Activator.CreateInstance(taskType)!;
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Theory]
    [Trait("Category", "EnvironmentViolation")]
    [MemberData(nameof(UnsafeEnvironmentTaskTypes))]
    public void UnsafeEnvironmentTask_IsAssignableFromMSBuildTask(Type taskType)
    {
        var task = Activator.CreateInstance(taskType)!;
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    #endregion
}