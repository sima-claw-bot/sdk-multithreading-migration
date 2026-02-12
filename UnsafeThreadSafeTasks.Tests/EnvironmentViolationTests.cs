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

    public void Dispose()
    {
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

    #region UsesEnvironmentGetVariable

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

    #region ReadsEnvironmentCurrentDirectory

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
}
