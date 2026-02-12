using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using UnsafeThreadSafeTasks.ComplexViolations;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for ComplexViolation unsafe tasks (batch 3):
/// AssemblyReferenceResolver, AsyncDelegateViolation, BaseClassHidesViolation, DeepCallChainPathResolve.
/// </summary>
public class ComplexViolationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _savedCwd;

    public ComplexViolationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ComplexViolationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _savedCwd = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        Directory.SetCurrentDirectory(_savedCwd);
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    #region AssemblyReferenceResolver

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ExecuteReturnsTrue()
    {
        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = _tempDir,
            BuildEngine = new ComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_EmptyAssemblyNames_ReturnsEmptyResults()
    {
        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = _tempDir,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ExistingAssembly_ResolvesToFullPath()
    {
        var dllPath = Path.Combine(_tempDir, "MyLib.dll");
        File.WriteAllText(dllPath, "fake-assembly");

        Directory.SetCurrentDirectory(_tempDir);

        // Use unique assembly name to avoid static cache interference
        var uniqueName = "MyLib_" + Guid.NewGuid().ToString("N");
        var uniqueDllPath = Path.Combine(_tempDir, uniqueName + ".dll");
        File.WriteAllText(uniqueDllPath, "fake-assembly");

        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = ".",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.True(Path.IsPathRooted(task.ResolvedPaths[0]));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_NonExistentAssembly_ReturnsEmptyString()
    {
        var uniqueName = "NonExistent_" + Guid.NewGuid().ToString("N");
        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = _tempDir,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_RelativePath_ResolvesAgainstCwd()
    {
        // Demonstrates the unsafe behavior: File.Exists with relative paths resolves against CWD
        var subDir = Path.Combine(_tempDir, "refs");
        Directory.CreateDirectory(subDir);

        var uniqueName = "CwdTest_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(subDir, uniqueName + ".dll"), "fake");

        Directory.SetCurrentDirectory(_tempDir);

        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "refs",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.NotEmpty(task.ResolvedPaths[0]);

        // Now change CWD to a directory where the refs subdir doesn't exist
        var otherDir = Path.Combine(_tempDir, "other");
        Directory.CreateDirectory(otherDir);
        Directory.SetCurrentDirectory(otherDir);

        // Static cache will still return the previously cached value
        var task2 = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "refs",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task2.Execute();

        // BUG: gets stale cached result from the first resolve
        Assert.Equal(task.ResolvedPaths[0], task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_StaticCache_ReturnsStaleCachedResults()
    {
        // The static cache serves results from a previous CWD context
        var uniqueName = "CacheTest_" + Guid.NewGuid().ToString("N");
        var dllPath = Path.Combine(_tempDir, uniqueName + ".dll");
        File.WriteAllText(dllPath, "fake");

        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = _tempDir,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        var firstResult = task.ResolvedPaths[0];

        // Second call with same name gets cached result regardless of ReferencePath
        var task2 = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = Path.Combine(_tempDir, "nonexistent"),
            BuildEngine = new ComplexTestBuildEngine()
        };
        task2.Execute();

        // BUG: returns stale cached result even though ReferencePath changed
        Assert.Equal(firstResult, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_MultipleAssemblies_ResolvesAll()
    {
        var name1 = "Multi1_" + Guid.NewGuid().ToString("N");
        var name2 = "Multi2_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(_tempDir, name1 + ".dll"), "fake1");
        File.WriteAllText(Path.Combine(_tempDir, name2 + ".dll"), "fake2");

        var task = new AssemblyReferenceResolver
        {
            AssemblyNames = new[] { name1, name2 },
            ReferencePath = _tempDir,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(2, task.ResolvedPaths.Length);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.NotEmpty(task.ResolvedPaths[1]);
    }

    #endregion

    #region AsyncDelegateViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ExecuteReturnsTrue()
    {
        var task = new AsyncDelegateViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ProducesNonEmptyResult()
    {
        var task = new AsyncDelegateViolation
        {
            RelativePath = "output.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.NotEmpty(task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ResultContainsRelativePath()
    {
        var task = new AsyncDelegateViolation
        {
            RelativePath = "myfile.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ResultIsAbsolutePath()
    {
        var task = new AsyncDelegateViolation
        {
            RelativePath = "relative.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.True(Path.IsPathRooted(task.Result));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ResolvesAgainstCwd_NotProjectDir()
    {
        // Demonstrates that the async delegate uses CWD at execution time
        Directory.SetCurrentDirectory(_tempDir);

        var task = new AsyncDelegateViolation
        {
            RelativePath = "test.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(_tempDir, "test.txt"), task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_CwdChangeBetweenCalls_ProducesDifferentResults()
    {
        var dir1 = Path.Combine(_tempDir, "dir1");
        var dir2 = Path.Combine(_tempDir, "dir2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        Directory.SetCurrentDirectory(dir1);
        var task1 = new AsyncDelegateViolation
        {
            RelativePath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new AsyncDelegateViolation
        {
            RelativePath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task2.Execute();

        // BUG: results depend on CWD at execution time, not capture time
        Assert.NotEqual(task1.Result, task2.Result);
        Assert.StartsWith(dir1, task1.Result);
        Assert.StartsWith(dir2, task2.Result);
    }

    #endregion

    #region BaseClassHidesViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_ExecuteReturnsTrue()
    {
        var task = new BaseClassHidesViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_AbsoluteInput_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var task = new BaseClassHidesViolation
        {
            InputPath = absPath,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(absPath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_RelativePath_ResolvesAgainstCwd()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new BaseClassHidesViolation
        {
            InputPath = "relative.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        // BUG: Path.GetFullPath in base class resolves against CWD
        Assert.Equal(Path.Combine(_tempDir, "relative.txt"), task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = Path.Combine(_tempDir, "proj1");
        var dir2 = Path.Combine(_tempDir, "proj2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        Directory.SetCurrentDirectory(dir1);
        var task1 = new BaseClassHidesViolation
        {
            InputPath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new BaseClassHidesViolation
        {
            InputPath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task2.Execute();

        // Different CWDs produce different results — violation hidden in base class
        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath);
        Assert.StartsWith(dir2, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_InheritsFromPathResolvingTaskBase()
    {
        var task = new BaseClassHidesViolation();
        Assert.IsAssignableFrom<PathResolvingTaskBase>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_CanonicalizesDotDotSegments()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new BaseClassHidesViolation
        {
            InputPath = Path.Combine("sub", "..", "file.txt"),
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(_tempDir, "file.txt"), task.ResolvedPath);
        Assert.DoesNotContain("..", task.ResolvedPath);
    }

    #endregion

    #region DeepCallChainPathResolve

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_ExecuteReturnsTrue()
    {
        var task = new DeepCallChainPathResolve
        {
            InputPath = "somefile.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_AbsoluteInput_ReturnsSamePath()
    {
        var absPath = Path.Combine(_tempDir, "file.txt");
        var task = new DeepCallChainPathResolve
        {
            InputPath = absPath,
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(absPath, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_EmptyInput_ReturnsEmptyString()
    {
        var task = new DeepCallChainPathResolve
        {
            InputPath = "",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_WhitespaceOnlyInput_ReturnsEmptyString()
    {
        var task = new DeepCallChainPathResolve
        {
            InputPath = "   ",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_RelativePath_ResolvesAgainstCwd()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new DeepCallChainPathResolve
        {
            InputPath = "relative.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        // BUG: Path.GetFullPath in NormalizePath (3 levels deep) resolves against CWD
        Assert.Equal(Path.Combine(_tempDir, "relative.txt"), task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_CwdChange_ProducesDifferentResults()
    {
        var dir1 = Path.Combine(_tempDir, "proj1");
        var dir2 = Path.Combine(_tempDir, "proj2");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        Directory.SetCurrentDirectory(dir1);
        var task1 = new DeepCallChainPathResolve
        {
            InputPath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task1.Execute();

        Directory.SetCurrentDirectory(dir2);
        var task2 = new DeepCallChainPathResolve
        {
            InputPath = "file.txt",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task2.Execute();

        // Different CWDs produce different results — violation 3 levels deep
        Assert.NotEqual(task1.OutputPath, task2.OutputPath);
        Assert.StartsWith(dir1, task1.OutputPath);
        Assert.StartsWith(dir2, task2.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_TrimsWhitespace()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new DeepCallChainPathResolve
        {
            InputPath = "  file.txt  ",
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(_tempDir, "file.txt"), task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_CanonicalizesDotDotSegments()
    {
        Directory.SetCurrentDirectory(_tempDir);

        var task = new DeepCallChainPathResolve
        {
            InputPath = Path.Combine("sub", "..", "file.txt"),
            BuildEngine = new ComplexTestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(_tempDir, "file.txt"), task.OutputPath);
        Assert.DoesNotContain("..", task.OutputPath);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for ComplexViolation tests.
/// </summary>
internal class ComplexTestBuildEngine : IBuildEngine
{
    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        IDictionary globalProperties, IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) { }
    public void LogMessageEvent(BuildMessageEventArgs e) { }
    public void LogWarningEvent(BuildWarningEventArgs e) { }
}
