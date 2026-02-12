using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class ComplexViolationTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ComplexViolation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    #region AssemblyReferenceResolver — basic execution and properties

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ExtendsTask()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_AssemblyNamesHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.AssemblyNames));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ReferencePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.ReferencePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ResolvedPathsHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.AssemblyReferenceResolver).GetProperty(nameof(UnsafeComplex.AssemblyReferenceResolver.ResolvedPaths));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_DefaultProperties()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver();

        Assert.Empty(task.AssemblyNames);
        Assert.Equal(string.Empty, task.ReferencePath);
        Assert.Empty(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = Array.Empty<string>(),
            ReferencePath = ".",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_ResolvesExistingAssembly()
    {
        var dir = CreateTempDir();
        var assemblyName = "TestAssembly";
        File.WriteAllText(Path.Combine(dir, assemblyName + ".dll"), "fake");

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { assemblyName },
            ReferencePath = dir,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.Contains(assemblyName, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_NonExistentAssembly_ReturnsEmpty()
    {
        var dir = CreateTempDir();

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "DoesNotExist" },
            ReferencePath = dir,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Single(task.ResolvedPaths);
        Assert.Equal(string.Empty, task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_StaticCache_ReturnsStaleCachedResults()
    {
        // The static cache means a second task instance with a different ReferencePath
        // may still get the cached result from the first call.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var assemblyName = $"CachedAsm_{Guid.NewGuid():N}";

        File.WriteAllText(Path.Combine(dir1, assemblyName + ".dll"), "fake1");
        File.WriteAllText(Path.Combine(dir2, assemblyName + ".dll"), "fake2");

        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { assemblyName },
            ReferencePath = dir1,
            BuildEngine = new FakeBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { assemblyName },
            ReferencePath = dir2,
            BuildEngine = new FakeBuildEngine()
        };
        task2.Execute();

        // Bug: second task gets the cached result from first task
        Assert.Equal(task1.ResolvedPaths[0], task2.ResolvedPaths[0]);
        Assert.Contains(dir1, task2.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AssemblyReferenceResolver_MultipleAssemblies_ResolvesSameCount()
    {
        var dir = CreateTempDir();
        var names = new[] { $"Asm1_{Guid.NewGuid():N}", $"Asm2_{Guid.NewGuid():N}" };
        foreach (var name in names)
            File.WriteAllText(Path.Combine(dir, name + ".dll"), "fake");

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = names,
            ReferencePath = dir,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(names.Length, task.ResolvedPaths.Length);
    }

    #endregion

    #region AsyncDelegateViolation — basic execution and properties

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_RelativePathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty(nameof(UnsafeComplex.AsyncDelegateViolation.RelativePath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_ResultHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty(nameof(UnsafeComplex.AsyncDelegateViolation.Result));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation();

        Assert.Equal(string.Empty, task.RelativePath);
        Assert.Equal(string.Empty, task.Result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_ResolvesAgainstProcessCwd()
    {
        var relativePath = "subdir/file.txt";
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = relativePath,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        // The result should contain the CWD since the delegate resolves lazily
        var cwd = Directory.GetCurrentDirectory();
        Assert.StartsWith(cwd, task.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.AsyncDelegateViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void AsyncDelegateViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.AsyncDelegateViolation),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region BaseClassHidesViolation — basic execution and properties

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_ExtendsTask()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_ExtendsPathResolvingTaskBase()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsAssignableFrom<UnsafeComplex.PathResolvingTaskBase>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_InputPathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.PathResolvingTaskBase).GetProperty(nameof(UnsafeComplex.PathResolvingTaskBase.InputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_ResolvedPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty(nameof(UnsafeComplex.BaseClassHidesViolation.ResolvedPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation();

        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_AbsolutePath_ReturnsSamePath()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = absPath,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(absPath, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_RelativePath_ResolvesAgainstCwd()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "relative/file.txt",
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        // Bug: resolves against CWD via base class's ResolvePath → Path.GetFullPath
        var cwd = Directory.GetCurrentDirectory();
        Assert.StartsWith(cwd, task.ResolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.BaseClassHidesViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void BaseClassHidesViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.BaseClassHidesViolation),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region DeepCallChainPathResolve — basic execution and properties

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_ExtendsTask()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_InputPathHasRequiredAttribute()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty(nameof(UnsafeComplex.DeepCallChainPathResolve.InputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_OutputPathHasOutputAttribute()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty(nameof(UnsafeComplex.DeepCallChainPathResolve.OutputPath));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_DefaultProperties()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve();

        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_Execute_ReturnsTrue()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "somefile.txt",
            BuildEngine = new FakeBuildEngine()
        };

        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_AbsolutePath_ReturnsSamePath()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = absPath,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(absPath, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_RelativePath_ResolvesAgainstCwd()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "relative/file.txt",
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        // Bug: Path.GetFullPath buried 3 levels deep resolves against CWD
        var cwd = Directory.GetCurrentDirectory();
        Assert.StartsWith(cwd, task.OutputPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_EmptyInput_ReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "",
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_WhitespaceOnlyInput_ReturnsEmpty()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "   ",
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(string.Empty, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_CanonicalizesPath()
    {
        var dir = CreateTempDir();
        var inputPath = Path.Combine(dir, "sub", "..", "file.txt");
        var expectedPath = Path.Combine(dir, "file.txt");

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = inputPath,
            BuildEngine = new FakeBuildEngine()
        };

        task.Execute();

        Assert.Equal(expectedPath, task.OutputPath);
        Assert.DoesNotContain("..", task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.DeepCallChainPathResolve).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    public void DeepCallChainPathResolve_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeComplex.DeepCallChainPathResolve),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region Concurrent execution tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public async Task AsyncDelegateViolation_ConcurrentExecution_AllResolveAgainstSameCwd()
    {
        const int concurrency = 4;
        var cwd = Directory.GetCurrentDirectory();

        var tasks = Enumerable.Range(0, concurrency).Select(i =>
        {
            return Task.Run(() =>
            {
                var task = new UnsafeComplex.AsyncDelegateViolation
                {
                    RelativePath = $"dir{i}/file.txt",
                    BuildEngine = new FakeBuildEngine()
                };

                task.Execute();
                return task.Result;
            });
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Bug: all resolve against the same process CWD
        foreach (var result in results)
        {
            Assert.StartsWith(cwd, result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public async Task DeepCallChainPathResolve_ConcurrentExecution_AllResolveAgainstSameCwd()
    {
        const int concurrency = 4;
        var cwd = Directory.GetCurrentDirectory();

        var tasks = Enumerable.Range(0, concurrency).Select(i =>
        {
            return Task.Run(() =>
            {
                var task = new UnsafeComplex.DeepCallChainPathResolve
                {
                    InputPath = $"project{i}/file.txt",
                    BuildEngine = new FakeBuildEngine()
                };

                task.Execute();
                return task.OutputPath;
            });
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Bug: all resolve against the same process CWD
        foreach (var result in results)
        {
            Assert.StartsWith(cwd, result, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public async Task BaseClassHidesViolation_ConcurrentExecution_AllResolveAgainstSameCwd()
    {
        const int concurrency = 4;
        var cwd = Directory.GetCurrentDirectory();

        var tasks = Enumerable.Range(0, concurrency).Select(i =>
        {
            return Task.Run(() =>
            {
                var task = new UnsafeComplex.BaseClassHidesViolation
                {
                    InputPath = $"project{i}/file.txt",
                    BuildEngine = new FakeBuildEngine()
                };

                task.Execute();
                return task.ResolvedPath;
            });
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        // Bug: all resolve against the same process CWD
        foreach (var result in results)
        {
            Assert.StartsWith(cwd, result, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion
}
