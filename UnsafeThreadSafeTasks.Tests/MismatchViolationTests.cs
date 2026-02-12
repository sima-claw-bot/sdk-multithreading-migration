#nullable enable
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeMismatch = UnsafeThreadSafeTasks.MismatchViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class MismatchViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mmtest_{Guid.NewGuid():N}");
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

    #region AttributeOnlyWithForbiddenApis

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_HasAttribute_ButNotInterface()
    {
        var taskType = typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis);

        Assert.True(Attribute.IsDefined(taskType, typeof(MSBuildMultiThreadableTaskAttribute)));
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_ResolvesRelativePathAgainstCwd()
    {
        var dir = CreateTempDir();
        var fileName = "testfile.txt";
        File.WriteAllText(Path.Combine(dir, fileName), "content");

        var task = new UnsafeMismatch.AttributeOnlyWithForbiddenApis
        {
            InputPath = fileName,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // BUG: File.Exists resolves against CWD, not the project directory
        // The file exists in dir, not in CWD, so File.Exists returns False
        Assert.Equal("False", task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_ConcurrentBothResolveAgainstCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var fileName = "testfile.txt";

        // Create files in both project dirs
        File.WriteAllText(Path.Combine(dir1, fileName), "content1");
        File.WriteAllText(Path.Combine(dir2, fileName), "content2");

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new UnsafeMismatch.AttributeOnlyWithForbiddenApis
            {
                InputPath = fileName,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new UnsafeMismatch.AttributeOnlyWithForbiddenApis
            {
                InputPath = fileName,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Unsafe: both resolve against CWD, so both return the same result
        Assert.Equal(result1, result2);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_AbsolutePathWorksCorrectly()
    {
        var dir = CreateTempDir();
        var filePath = Path.Combine(dir, "exists.txt");
        File.WriteAllText(filePath, "content");

        var task = new UnsafeMismatch.AttributeOnlyWithForbiddenApis
        {
            InputPath = filePath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Absolute paths work because File.Exists doesn't need CWD resolution
        Assert.Equal("True", task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_InheritsFromMSBuildTask()
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_HasRequiredAndOutputProperties()
    {
        var inputProp = typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis).GetProperty("InputPath");
        var resultProp = typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis).GetProperty("Result");

        Assert.NotNull(inputProp);
        Assert.NotNull(resultProp);
        Assert.NotNull(inputProp!.GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(resultProp!.GetCustomAttribute<OutputAttribute>());
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_Unsafe_ExecuteReturnsTrueAndSetsResult()
    {
        var task = new UnsafeMismatch.AttributeOnlyWithForbiddenApis
        {
            InputPath = "nonexistent_file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(
            typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void AttributeOnlyWithForbiddenApis_HasNoTaskEnvironmentProperty()
    {
        // Without IMultiThreadableTask, there is no TaskEnvironment property
        var prop = typeof(UnsafeMismatch.AttributeOnlyWithForbiddenApis)
            .GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    #endregion

    #region IgnoresTaskEnvironment

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_ImplementsInterface_ButIgnoresIt()
    {
        var taskType = typeof(UnsafeMismatch.IgnoresTaskEnvironment);

        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        // Mismatch: implements IMultiThreadableTask but lacks the attribute
        Assert.False(Attribute.IsDefined(
            typeof(UnsafeMismatch.IgnoresTaskEnvironment),
            typeof(MSBuildMultiThreadableTaskAttribute)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_ResolvesAgainstCwdNotProjectDir()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeMismatch.IgnoresTaskEnvironment
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

    [Theory]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(typeof(UnsafeMismatch.IgnoresTaskEnvironment))]
    public void IgnoresTaskEnvironment_Unsafe_ConcurrentBothResolveAgainstCwd(Type taskType)
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
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_AbsoluteInputUnchanged()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeMismatch.IgnoresTaskEnvironment
        {
            InputPath = absPath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Absolute paths are already resolved, so Path.GetFullPath returns them as-is
        Assert.Equal(absPath, task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_InheritsFromMSBuildTask()
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(typeof(UnsafeMismatch.IgnoresTaskEnvironment)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_HasRequiredAndOutputProperties()
    {
        var inputProp = typeof(UnsafeMismatch.IgnoresTaskEnvironment).GetProperty("InputPath");
        var resultProp = typeof(UnsafeMismatch.IgnoresTaskEnvironment).GetProperty("Result");

        Assert.NotNull(inputProp);
        Assert.NotNull(resultProp);
        Assert.NotNull(inputProp!.GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(resultProp!.GetCustomAttribute<OutputAttribute>());
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_ExecuteReturnsTrueAndSetsResult()
    {
        var task = new UnsafeMismatch.IgnoresTaskEnvironment
        {
            InputPath = "some_file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void IgnoresTaskEnvironment_Unsafe_TaskEnvironmentValueHasNoEffect()
    {
        var projDir = CreateTempDir();
        var relativePath = "sub\\file.txt";

        var taskWithEnv = new UnsafeMismatch.IgnoresTaskEnvironment
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = new MockBuildEngine()
        };

        var taskWithoutEnv = new UnsafeMismatch.IgnoresTaskEnvironment
        {
            InputPath = relativePath,
            BuildEngine = new MockBuildEngine()
        };

        taskWithEnv.Execute();
        taskWithoutEnv.Execute();

        // BUG: TaskEnvironment is ignored, so both return the same CWD-based result
        Assert.Equal(taskWithEnv.Result, taskWithoutEnv.Result);
        Assert.Equal(Path.GetFullPath(relativePath), taskWithEnv.Result);
    }

    #endregion

    #region NullChecksTaskEnvironment

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_ImplementsInterface()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(
            typeof(UnsafeMismatch.NullChecksTaskEnvironment)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_WithTaskEnvironment_ResolvesCorrectly()
    {
        var projDir = CreateTempDir();
        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "sub\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // When TaskEnvironment is provided, it uses the safe path
        Assert.StartsWith(projDir, task.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_WithNullTaskEnv_FallsBackToCwd()
    {
        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            TaskEnvironment = null!,
            InputPath = "sub\\file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // BUG: falls back to Path.GetFullPath which resolves against CWD
        Assert.Equal(Path.GetFullPath("sub\\file.txt"), task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_ConcurrentWithNullEnv_BothResolveAgainstCwd()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = Path.Combine("subdir", "file.txt");
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new UnsafeMismatch.NullChecksTaskEnvironment
            {
                TaskEnvironment = null!,
                InputPath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new UnsafeMismatch.NullChecksTaskEnvironment
            {
                TaskEnvironment = null!,
                InputPath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Unsafe: both fall back to CWD, so both produce the same result
        Assert.Equal(result1, result2);
        Assert.Equal(Path.GetFullPath(relativePath), result1);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_ConcurrentWithEnv_ResolvesToOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        string relativePath = Path.Combine("subdir", "file.txt");
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new UnsafeMismatch.NullChecksTaskEnvironment
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                InputPath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new UnsafeMismatch.NullChecksTaskEnvironment
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                InputPath = relativePath,
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // When TaskEnvironment is provided, it resolves correctly to each project dir
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(result1, result2);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_DefaultFieldValue_IsNull()
    {
        // The default field value is null! — verify the fallback path is exercised by default
        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            InputPath = "test.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Default TaskEnvironment is null, so it falls back to Path.GetFullPath
        Assert.Equal(Path.GetFullPath("test.txt"), task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_InheritsFromMSBuildTask()
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(typeof(UnsafeMismatch.NullChecksTaskEnvironment)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_HasRequiredAndOutputProperties()
    {
        var inputProp = typeof(UnsafeMismatch.NullChecksTaskEnvironment).GetProperty("InputPath");
        var resultProp = typeof(UnsafeMismatch.NullChecksTaskEnvironment).GetProperty("Result");

        Assert.NotNull(inputProp);
        Assert.NotNull(resultProp);
        Assert.NotNull(inputProp!.GetCustomAttribute<RequiredAttribute>());
        Assert.NotNull(resultProp!.GetCustomAttribute<OutputAttribute>());
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_ExecuteReturnsTrueAndSetsResult()
    {
        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            InputPath = "some_file.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        // Mismatch: implements IMultiThreadableTask but lacks the attribute
        Assert.False(Attribute.IsDefined(
            typeof(UnsafeMismatch.NullChecksTaskEnvironment),
            typeof(MSBuildMultiThreadableTaskAttribute)));
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_WithNullTaskEnv_MatchesCwdBehavior()
    {
        var relativePath = "deep\\nested\\file.txt";

        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            TaskEnvironment = null!,
            InputPath = relativePath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Null fallback resolves identically to direct Path.GetFullPath (CWD-based)
        var expected = Path.GetFullPath(relativePath);
        Assert.Equal(expected, task.Result);
    }

    [Fact]
    [Trait("Category", "MismatchViolation")]
    [Trait("Target", "Unsafe")]
    public void NullChecksTaskEnvironment_Unsafe_WithAbsolutePathAndNullEnv_ReturnsAbsolute()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeMismatch.NullChecksTaskEnvironment
        {
            TaskEnvironment = null!,
            InputPath = absPath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Absolute paths work even in the unsafe fallback path
        Assert.Equal(absPath, task.Result);
    }

    #endregion
}
