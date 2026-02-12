#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using UnsafeThreadSafeTasks.EnvironmentViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests;

public class EnvironmentViolationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _originalCwd;

    public EnvironmentViolationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"EnvViolation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _originalCwd = Environment.CurrentDirectory;
    }

    public void Dispose()
    {
        // Restore original state
        Environment.CurrentDirectory = _originalCwd;

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region ReadsEnvironmentCurrentDirectory

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void ReadsEnvironmentCurrentDirectory_Execute_ReturnsCurrentDirectory()
    {
        var task = new ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(Environment.CurrentDirectory, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void ReadsEnvironmentCurrentDirectory_Execute_ReturnsProcessGlobalCwd()
    {
        // Demonstrates the bug: the task reads process-global state
        Environment.CurrentDirectory = _tempDir;

        var task = new ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(_tempDir, task.Result);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public async Task ReadsEnvironmentCurrentDirectory_ConcurrentExecution_AllReadSameGlobalState()
    {
        // Both tasks see the same process-global CWD regardless of intended project directory
        var dir1 = Path.Combine(_tempDir, "proj1");
        var dir2 = Path.Combine(_tempDir, "proj2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = Task.Run(() =>
        {
            // Imagine this task should read dir1 as its project directory
            barrier.SignalAndWait();
            var task = new ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
            task.Execute();
            result1 = task.Result;
        });

        var t2 = Task.Run(() =>
        {
            // Imagine this task should read dir2 as its project directory
            barrier.SignalAndWait();
            var task = new ReadsEnvironmentCurrentDirectory { BuildEngine = new MockBuildEngine() };
            task.Execute();
            result2 = task.Result;
        });

        await Task.WhenAll(t1, t2);

        // Unsafe: both tasks read the same process-global CWD
        Assert.Equal(result1, result2);
    }

    #endregion

    #region SetsEnvironmentCurrentDirectory

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_Execute_ChangesGlobalCwdAndReadsFile()
    {
        var filePath = "testfile.txt";
        File.WriteAllText(Path.Combine(_tempDir, filePath), "hello from temp");

        var task = new SetsEnvironmentCurrentDirectory
        {
            NewDirectory = _tempDir,
            RelativeFilePath = filePath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello from temp", task.Result);
        // Side effect: process-global CWD is now changed
        Assert.Equal(_tempDir, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_Execute_MutatesProcessGlobalState()
    {
        var originalCwd = Environment.CurrentDirectory;
        var dir = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "data.txt"), "content");

        var task = new SetsEnvironmentCurrentDirectory
        {
            NewDirectory = dir,
            RelativeFilePath = "data.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Demonstrates the bug: CWD has been mutated for the entire process
        Assert.NotEqual(originalCwd, Environment.CurrentDirectory);
        Assert.Equal(dir, Environment.CurrentDirectory);
    }

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void SetsEnvironmentCurrentDirectory_WithNonExistentFile_Throws()
    {
        var task = new SetsEnvironmentCurrentDirectory
        {
            NewDirectory = _tempDir,
            RelativeFilePath = "nonexistent.txt",
            BuildEngine = new MockBuildEngine()
        };

        Assert.ThrowsAny<Exception>(() => task.Execute());
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
            var task = new UsesEnvironmentGetVariable
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
        var task = new UsesEnvironmentGetVariable
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
                var task = new UsesEnvironmentGetVariable
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
                var task = new UsesEnvironmentGetVariable
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

    #endregion

    #region UsesEnvironmentSetVariable

    [Fact]
    [Trait("Category", "EnvironmentViolation")]
    public void UsesEnvironmentSetVariable_Execute_SetsAndReadsProcessEnvironmentVariable()
    {
        var varName = $"TEST_ENV_SET_{Guid.NewGuid():N}";
        try
        {
            var task = new UsesEnvironmentSetVariable
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

            var task = new UsesEnvironmentSetVariable
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
                var task = new UsesEnvironmentSetVariable
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
                var task = new UsesEnvironmentSetVariable
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
            var task = new UsesEnvironmentSetVariable
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

    #endregion
}
