using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;

namespace UnsafeThreadSafeTasks.Tests;

public class ComplexViolationTests : IDisposable
{
    private readonly List<string> _tempDirs = new();
    private readonly string _savedCwd;

    public ComplexViolationTests()
    {
        _savedCwd = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_savedCwd);
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ComplexTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    #region AssemblyReferenceResolver

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "TestLib" },
            ReferencePath = "libs",
            BuildEngine = new StubBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ResolvedPathsMatchAssemblyCount()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "Lib1", "Lib2", "Lib3" },
            ReferencePath = "refs",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(3, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ExistingAssembly_ResolvesToFullPath()
    {
        var dir = CreateTempDir();
        var libDir = Path.Combine(dir, "libs");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "MyLib.dll"), "fake");

        Directory.SetCurrentDirectory(dir);

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "MyLib" },
            ReferencePath = "libs",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.True(Path.IsPathRooted(task.ResolvedPaths[0]));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_NonExistentAssembly_ReturnsEmpty()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "DoesNotExist" },
            ReferencePath = "nonexistent",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_RelativePathResolvesAgainstCwd()
    {
        // Demonstrates the CWD-dependent bug: changing CWD changes resolution
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var libDir1 = Path.Combine(dir1, "refs");
        Directory.CreateDirectory(libDir1);
        File.WriteAllText(Path.Combine(libDir1, "Asm.dll"), "fake");

        // From dir1, the assembly is found
        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "Asm_cwd_test" },
            ReferencePath = "refs",
            BuildEngine = new StubBuildEngine()
        };
        task1.Execute();
        // Use unique name to avoid static cache interference
        // From dir2, "refs/Asm.dll" does not exist
        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "Asm_cwd_test2" },
            ReferencePath = "refs",
            BuildEngine = new StubBuildEngine()
        };
        task2.Execute();

        // task2 cannot find it because CWD changed
        Assert.Equal(string.Empty, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_StaticCacheServesStaleEntries()
    {
        // Demonstrates the static cache bug: once resolved, the cache returns
        // the same result even when CWD changes.
        var dir = CreateTempDir();
        var libDir = Path.Combine(dir, "cache_test");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "Cached.dll"), "fake");

        Directory.SetCurrentDirectory(dir);

        var uniqueName = $"Cached_{Guid.NewGuid():N}";
        File.WriteAllText(Path.Combine(libDir, uniqueName + ".dll"), "fake");

        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "cache_test",
            BuildEngine = new StubBuildEngine()
        };
        task1.Execute();
        var firstResult = task1.ResolvedPaths[0];

        // Change CWD to a different directory
        var dir2 = CreateTempDir();
        Directory.SetCurrentDirectory(dir2);

        // Second resolution uses the static cache and returns the stale result
        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "cache_test",
            BuildEngine = new StubBuildEngine()
        };
        task2.Execute();

        // The static cache returns the same (stale) path from the first resolution
        Assert.Equal(firstResult, task2.ResolvedPaths[0]);
    }

    #endregion

    #region AsyncDelegateViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "subdir",
            BuildEngine = new StubBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ResultContainsRelativePath()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "output",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.EndsWith("output", task.Result.Replace('\\', '/'));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ResolvesAgainstProcessCwd()
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "file.txt",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        // The result should use the CWD that was active during delegate execution
        Assert.Contains(Path.GetFileName(dir), task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_CwdChangeAffectsResult()
    {
        // Demonstrates the bug: the delegate resolves against current CWD at execution time
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);

        var task1 = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "test.txt",
            BuildEngine = new StubBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);

        var task2 = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "test.txt",
            BuildEngine = new StubBuildEngine()
        };
        task2.Execute();

        // Different CWDs produce different results for the same relative path
        Assert.NotEqual(task1.Result, task2.Result);
    }

    #endregion

    #region BaseClassHidesViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "some_path",
            BuildEngine = new StubBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_AbsoluteInput_ReturnsSamePath()
    {
        var absPath = CreateTempDir();

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = absPath,
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(absPath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_RelativeInput_ResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "relative",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, "relative"));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "data",
            BuildEngine = new StubBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "data",
            BuildEngine = new StubBuildEngine()
        };
        task2.Execute();

        // The base class resolves via Path.GetFullPath, which is CWD-dependent
        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_OutputIsRooted()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "relative_path",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.True(Path.IsPathRooted(task.ResolvedPath));
    }

    #endregion

    #region DeepCallChainPathResolve

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "some_path",
            BuildEngine = new StubBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_EmptyInput_ReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_WhitespaceInput_ReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "   ",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_AbsoluteInput_ReturnsSamePath()
    {
        var absPath = CreateTempDir();

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = absPath,
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.Equal(absPath, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_RelativeInput_ResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "subdir",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, "subdir"));
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "output",
            BuildEngine = new StubBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "output",
            BuildEngine = new StubBuildEngine()
        };
        task2.Execute();

        // Deep call chain hides Path.GetFullPath which is CWD-dependent
        Assert.NotEqual(task1.OutputPath, task2.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_OutputIsRooted()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "relative_path",
            BuildEngine = new StubBuildEngine()
        };

        task.Execute();

        Assert.True(Path.IsPathRooted(task.OutputPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_TrimsInputBeforeResolving()
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var taskTrimmed = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "test",
            BuildEngine = new StubBuildEngine()
        };
        taskTrimmed.Execute();

        var taskPadded = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "  test  ",
            BuildEngine = new StubBuildEngine()
        };
        taskPadded.Execute();

        Assert.Equal(taskTrimmed.OutputPath, taskPadded.OutputPath);
    }

    #endregion
}

internal class StubBuildEngine : IBuildEngine
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
