using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class ComplexViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cvtest_{Guid.NewGuid():N}");
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

    #region AssemblyReferenceResolver — static cache and relative-path bugs

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ExtendsTask()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_AssemblyNamesHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.AssemblyNames));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ReferencePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.ReferencePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_ResolvedPathsHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.ResolvedPaths));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_DefaultProperties()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();
        Assert.Empty(task.AssemblyNames);
        Assert.Equal(string.Empty, task.ReferencePath);
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "NonExistent" },
            ReferencePath = "fakepath",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Execute_ResolvesExistingAssembly()
    {
        var dir = CreateTempDir();
        var asmName = $"TestAsm_{Guid.NewGuid():N}";
        File.WriteAllBytes(Path.Combine(dir, asmName + ".dll"), Array.Empty<byte>());

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { asmName },
            ReferencePath = dir,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Single(task.ResolvedPaths);
        Assert.Contains(asmName, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Execute_ReturnsEmptyForMissingAssembly()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "DoesNotExist_" + Guid.NewGuid().ToString("N") },
            ReferencePath = CreateTempDir(),
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Single(task.ResolvedPaths);
        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_StaticCache_ServesStaleEntries()
    {
        // The static cache means that if assembly "X" was resolved in one invocation,
        // subsequent invocations reuse the cached value even with a different ReferencePath.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var asmName = $"CachedAsm_{Guid.NewGuid():N}";
        File.WriteAllBytes(Path.Combine(dir1, asmName + ".dll"), Array.Empty<byte>());
        // Do NOT create the assembly in dir2

        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { asmName },
            ReferencePath = dir1,
            BuildEngine = new MockBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { asmName },
            ReferencePath = dir2,
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();

        // Bug: task2 gets the cached result from task1 even though the assembly doesn't exist in dir2
        Assert.Equal(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
        Assert.NotEqual(string.Empty, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.AssemblyReferenceResolver),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region AsyncDelegateViolation — CWD captured at wrong time

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_RelativePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty(nameof(UnsafeComplex.AsyncDelegateViolation.RelativePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ResultHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty(nameof(UnsafeComplex.AsyncDelegateViolation.Result));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "subdir",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_Execute_ResolvesAgainstProcessCwd()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "myfile.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Bug: resolves against the process CWD, not a project directory
        var expected = Path.Combine(Directory.GetCurrentDirectory(), "myfile.txt");
        Assert.Equal(expected, task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.AsyncDelegateViolation),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region BaseClassHidesViolation — violation hidden in base class

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_ExtendsPathResolvingTaskBase()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsAssignableFrom<UnsafeComplex.PathResolvingTaskBase>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_InputPathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty(nameof(UnsafeComplex.BaseClassHidesViolation.InputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_ResolvedPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty(nameof(UnsafeComplex.BaseClassHidesViolation.ResolvedPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Execute_ResolvesRelativePathAgainstCwd()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "relative.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Bug: Path.GetFullPath in the base class resolves against process CWD
        var expected = Path.GetFullPath("relative.txt");
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Execute_AbsolutePathPassedThrough()
    {
        var dir = CreateTempDir();
        var absolutePath = Path.Combine(dir, "abs.txt");

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = absolutePath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(absolutePath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.BaseClassHidesViolation),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region DeepCallChainPathResolve — violation buried in deep call chain

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_ExtendsTask()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_InputPathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty(nameof(UnsafeComplex.DeepCallChainPathResolve.InputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_OutputPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty(nameof(UnsafeComplex.DeepCallChainPathResolve.OutputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_DefaultProperties()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();
        Assert.True(result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_ResolvesRelativePathAgainstCwd()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "deep/nested/file.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Bug: deep in the call chain, Path.GetFullPath resolves against process CWD
        var expected = Path.GetFullPath("deep/nested/file.txt");
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_AbsolutePathPassedThrough()
    {
        var dir = CreateTempDir();
        var absolutePath = Path.Combine(dir, "abs.txt");

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = absolutePath,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(absolutePath, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_EmptyInputReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_WhitespaceOnlyInputReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "   ",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // After trimming, empty string → returns empty
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Execute_TrimsInputBeforeResolving()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "  somefile.txt  ",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath("somefile.txt");
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.DeepCallChainPathResolve),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region All ComplexViolation tasks — common structural checks

    public static IEnumerable<object[]> AllComplexViolationTypes()
    {
        yield return new object[] { typeof(UnsafeComplex.AssemblyReferenceResolver) };
        yield return new object[] { typeof(UnsafeComplex.AsyncDelegateViolation) };
        yield return new object[] { typeof(UnsafeComplex.BaseClassHidesViolation) };
        yield return new object[] { typeof(UnsafeComplex.DeepCallChainPathResolve) };
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(AllComplexViolationTypes))]
    public void ComplexViolationType_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(AllComplexViolationTypes))]
    public void ComplexViolationType_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(AllComplexViolationTypes))]
    public void ComplexViolationType_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(AllComplexViolationTypes))]
    public void ComplexViolationType_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.ComplexViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(AllComplexViolationTypes))]
    public void ComplexViolationType_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    #endregion
}
