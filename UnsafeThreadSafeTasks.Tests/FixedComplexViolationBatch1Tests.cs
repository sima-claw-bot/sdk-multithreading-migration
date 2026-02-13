using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using FixedComplex = FixedThreadSafeTasks.ComplexViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for fixed ComplexViolation tasks (batch 1):
/// TaskAlpha01, TaskAlpha02, TaskAlpha03, TaskAlpha04.
/// </summary>
public class FixedComplexViolationBatch1Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fcvb1test_{Guid.NewGuid():N}");
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

    #region TaskAlpha01 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_EmptyAssemblyNames_ReturnsEmptyResults()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_ExistingAssembly_ResolvesAgainstProjectDir()
    {
        var projDir = CreateTempDir();
        var refsDir = Path.Combine(projDir, "refs");
        Directory.CreateDirectory(refsDir);
        File.WriteAllText(Path.Combine(refsDir, "MyLib.dll"), "fake-assembly");

        var task = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            AssemblyNames = new[] { "MyLib" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.True(Path.IsPathRooted(task.ResolvedPaths[0]));
        Assert.StartsWith(projDir, task.ResolvedPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_NonExistentAssembly_ReturnsEmptyString()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            AssemblyNames = new[] { "NonExistent" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir1, "refs"));
        Directory.CreateDirectory(Path.Combine(dir2, "refs"));
        File.WriteAllText(Path.Combine(dir1, "refs", "Lib.dll"), "fake1");
        File.WriteAllText(Path.Combine(dir2, "refs", "Lib.dll"), "fake2");

        var task1 = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            AssemblyNames = new[] { "Lib" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            AssemblyNames = new[] { "Lib" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
        Assert.StartsWith(dir1, task1.ResolvedPaths[0], StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedPaths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_MultipleAssemblies_ResolvesAll()
    {
        var projDir = CreateTempDir();
        var refsDir = Path.Combine(projDir, "refs");
        Directory.CreateDirectory(refsDir);
        File.WriteAllText(Path.Combine(refsDir, "Lib1.dll"), "fake1");
        File.WriteAllText(Path.Combine(refsDir, "Lib2.dll"), "fake2");

        var task = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            AssemblyNames = new[] { "Lib1", "Lib2" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(2, task.ResolvedPaths.Length);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.NotEmpty(task.ResolvedPaths[1]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_InstanceCache_NoCrossTaskContamination()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir1, "refs"));
        File.WriteAllText(Path.Combine(dir1, "refs", "Shared.dll"), "fake");

        var task1 = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            AssemblyNames = new[] { "Shared" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task1.Execute();
        Assert.NotEmpty(task1.ResolvedPaths[0]);

        // Second task with different project dir where the assembly doesn't exist
        var task2 = new FixedComplex.TaskAlpha01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            AssemblyNames = new[] { "Shared" },
            ReferencePath = "refs",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task2.Execute();

        // Instance cache means no stale results from task1
        Assert.Equal(string.Empty, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha01)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha01)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AssemblyReferenceResolver_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(dir1, "refs"));
        Directory.CreateDirectory(Path.Combine(dir2, "refs"));
        File.WriteAllText(Path.Combine(dir1, "refs", "ConcLib.dll"), "fake1");
        File.WriteAllText(Path.Combine(dir2, "refs", "ConcLib.dll"), "fake2");

        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha01
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                AssemblyNames = new[] { "ConcLib" },
                ReferencePath = "refs",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedPaths[0];
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha01
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                AssemblyNames = new[] { "ConcLib" },
                ReferencePath = "refs",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedPaths[0];
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region TaskAlpha02 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "somefile.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "output.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.Result, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.Result));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_ResultContainsRelativePath()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativePath = "myfile.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.TaskAlpha02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            RelativePath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            RelativePath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.Result, task2.Result);
        Assert.StartsWith(dir1, task1.Result, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha02)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha02)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void AsyncDelegateViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha02
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                RelativePath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.Result;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha02
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                RelativePath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.Result;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region TaskAlpha03 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "somefile.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "subdir\\file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.TaskAlpha03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            InputPath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            InputPath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha03)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha03)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_InheritsFromPathResolvingTaskBase()
    {
        var task = new FixedComplex.TaskAlpha03();
        Assert.IsAssignableFrom<FixedComplex.PathResolvingTaskBase>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void BaseClassHidesViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha03
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                InputPath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedPath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha03
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                InputPath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedPath;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region TaskAlpha04 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "somefile.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "subdir\\file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.OutputPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_EmptyInput_ReturnsEmptyString()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_WhitespaceOnlyInput_ReturnsEmptyString()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "   ",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            InputPath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha04
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            InputPath = "file.txt",
            BuildEngine = new FixedBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.OutputPath, task2.OutputPath);
        Assert.StartsWith(dir1, task1.OutputPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.OutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha04)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha04)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void DeepCallChainPathResolve_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha04
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                InputPath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.OutputPath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha04
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                InputPath = "file.txt",
                BuildEngine = new FixedBatch1TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.OutputPath;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1!, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2!, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for FixedComplexViolation batch 1 tests.
/// </summary>
internal class FixedBatch1TestBuildEngine : Microsoft.Build.Framework.IBuildEngine
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
