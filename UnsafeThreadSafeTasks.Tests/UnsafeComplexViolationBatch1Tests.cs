using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for unsafe ComplexViolation tasks (batch 1):
/// AssemblyReferenceResolver, AsyncDelegateViolation, BaseClassHidesViolation, DeepCallChainPathResolve.
/// These tests verify the CWD-dependent and static-state bugs present in the unsafe versions.
/// </summary>
public class UnsafeComplexViolationBatch1Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public UnsafeComplexViolationBatch1Tests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ucvb1test_{Guid.NewGuid():N}");
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

    #region AssemblyReferenceResolver — CWD + static cache bugs

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_EmptyAssemblyNames_ReturnsEmptyResults()
    {
        ClearAssemblyResolverCache();
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = "refs",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_MissingAssembly_ReturnsEmptyString()
    {
        ClearAssemblyResolverCache();
        var uniqueName = "Missing_" + Guid.NewGuid().ToString("N");
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = CreateTempDir(),
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Single(task.ResolvedPaths);
        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_ExistingAssembly_FindsViaCwd()
    {
        ClearAssemblyResolverCache();
        var dir = CreateTempDir();
        var refsDir = Path.Combine(dir, "refs");
        Directory.CreateDirectory(refsDir);
        var uniqueName = "Found_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(refsDir, uniqueName + ".dll"), "fake");

        // BUG: File.Exists uses relative path, which resolves against CWD
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "refs",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.True(Path.IsPathRooted(task.ResolvedPaths[0]));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_CwdDeterminesFileResolution()
    {
        ClearAssemblyResolverCache();
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var refsDir = Path.Combine(dir1, "refs");
        Directory.CreateDirectory(refsDir);
        var uniqueName = "CwdTest_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(refsDir, uniqueName + ".dll"), "fake");

        // CWD is dir1: File.Exists("refs/X.dll") finds the file
        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "refs",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task1.Execute();
        Assert.NotEmpty(task1.ResolvedPaths[0]);

        // CWD is dir2: File.Exists("refs/X.dll") won't find the file, but static cache serves stale result
        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { uniqueName },
            ReferencePath = "refs",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task2.Execute();

        // BUG: static cache returns stale result from first resolve
        Assert.Equal(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_MultipleAssemblies_ResolvesAll()
    {
        ClearAssemblyResolverCache();
        var dir = CreateTempDir();
        var name1 = "Multi1_" + Guid.NewGuid().ToString("N");
        var name2 = "Multi2_" + Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(dir, name1 + ".dll"), "fake1");
        File.WriteAllText(Path.Combine(dir, name2 + ".dll"), "fake2");

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { name1, name2 },
            ReferencePath = dir,
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(2, task.ResolvedPaths.Length);
        Assert.NotEmpty(task.ResolvedPaths[0]);
        Assert.NotEmpty(task.ResolvedPaths[1]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.AssemblyReferenceResolver)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AssemblyReferenceResolver_HasStaticCache()
    {
        var field = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetField("ResolvedAssemblyCache", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        Assert.True(field!.IsStatic);
    }

    #endregion

    #region AsyncDelegateViolation — CWD captured at delegate execution time

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_ResultContainsRelativePath()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "myfile.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Contains("myfile.txt", task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_ResolvesAgainstProcessCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "deferred.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        // BUG: uses Directory.GetCurrentDirectory() inside the delegate
        var expected = Path.Combine(dir, "deferred.txt");
        Assert.Equal(expected, task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_CwdChangeBetweenCalls_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task2.Execute();

        // Different CWDs produce different results — CWD captured at execution time
        Assert.NotEqual(task1.Result, task2.Result);
        Assert.StartsWith(dir1, task1.Result);
        Assert.StartsWith(dir2, task2.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.AsyncDelegateViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.AsyncDelegateViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void AsyncDelegateViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.Result);
    }

    #endregion

    #region BaseClassHidesViolation — Path.GetFullPath hidden in base class

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_ResolvesRelativePathAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "hidden.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        // BUG: base class uses Path.GetFullPath which resolves against CWD
        var expected = Path.GetFullPath(Path.Combine(dir, "hidden.txt"));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_AbsolutePathPassedThrough()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), "abs_test.txt");
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = absolutePath,
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(absolutePath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedPath, task2.ResolvedPath);
        Assert.StartsWith(dir1, task1.ResolvedPath);
        Assert.StartsWith(dir2, task2.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_InheritsFromPathResolvingTaskBase()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsAssignableFrom<UnsafeComplex.PathResolvingTaskBase>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.BaseClassHidesViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.BaseClassHidesViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void BaseClassHidesViolation_CanonicalizesDotDotSegments()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = Path.Combine("sub", "..", "file.txt"),
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(dir, "file.txt"), task.ResolvedPath);
        Assert.DoesNotContain("..", task.ResolvedPath);
    }

    #endregion

    #region DeepCallChainPathResolve — Path.GetFullPath 3+ levels deep

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "somefile.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_ResolvesRelativePathAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "deep.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        // BUG: NormalizePath (3 levels deep) calls Path.GetFullPath against CWD
        var expected = Path.GetFullPath(Path.Combine(dir, "deep.txt"));
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_EmptyInput_ReturnsEmptyString()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_WhitespaceOnlyInput_ReturnsEmptyString()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "   ",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_TrimsInputBeforeResolving()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "  trimmed.txt  ",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, "trimmed.txt"));
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_AbsolutePathPassedThrough()
    {
        var dir = CreateTempDir();
        var absolutePath = Path.Combine(dir, "abs.txt");

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = absolutePath,
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(absolutePath, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.OutputPath, task2.OutputPath);
        Assert.StartsWith(dir1, task1.OutputPath);
        Assert.StartsWith(dir2, task2.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.DeepCallChainPathResolve)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.DeepCallChainPathResolve)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_DefaultProperties()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    public void DeepCallChainPathResolve_CanonicalizesDotDotSegments()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = Path.Combine("sub", "..", "file.txt"),
            BuildEngine = new UnsafeBatch1TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(Path.Combine(dir, "file.txt"), task.OutputPath);
        Assert.DoesNotContain("..", task.OutputPath);
    }

    #endregion

    #region Cross-cutting structural checks for all batch 1 types

    public static IEnumerable<object[]> Batch1TaskTypes()
    {
        yield return new object[] { typeof(UnsafeComplex.AssemblyReferenceResolver) };
        yield return new object[] { typeof(UnsafeComplex.AsyncDelegateViolation) };
        yield return new object[] { typeof(UnsafeComplex.BaseClassHidesViolation) };
        yield return new object[] { typeof(UnsafeComplex.DeepCallChainPathResolve) };
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.ComplexViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "1")]
    [MemberData(nameof(Batch1TaskTypes))]
    public void Batch1Type_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    #endregion

    #region Helpers

    private static void ClearAssemblyResolverCache()
    {
        var cacheField = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetField("ResolvedAssemblyCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as Dictionary<string, string>;
        cache?.Clear();
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for UnsafeComplexViolation batch 1 tests.
/// </summary>
internal class UnsafeBatch1TestBuildEngine : IBuildEngine
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
