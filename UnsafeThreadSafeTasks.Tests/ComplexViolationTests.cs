using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using UnsafeThreadSafeTasks.ComplexViolations;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class ComplexViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public ComplexViolationTests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cvtest_{Guid.NewGuid():N}");
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

    #region DictionaryCacheViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.DictionaryCacheViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DictionaryCacheViolation_Unsafe_ResolvesAgainstProcessCwd()
    {
        // Clear static cache via reflection
        var cacheField = typeof(UnsafeComplex.DictionaryCacheViolation)
            .GetField("PathCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as ConcurrentDictionary<string, string>;
        cache?.Clear();

        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.DictionaryCacheViolation
        {
            RelativePath = "shared.txt",
            BuildEngine = new MockBuildEngine()
        };
        task1.Execute();
        var resolved1 = task1.ResolvedPath;

        // Change CWD and run with the same relative key — cached value is returned
        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.DictionaryCacheViolation
        {
            RelativePath = "shared.txt",
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();
        var resolved2 = task2.ResolvedPath;

        // BUG: both resolve to the same path (the first CWD's resolution)
        Assert.Equal(resolved1, resolved2);
        // The second result should have been based on dir2, but it's based on dir1
        Assert.Contains(dir1, resolved1);
    }

    #endregion

    #region EventHandlerViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.EventHandlerViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_Unsafe_UsesProcessCwdAtFireTime()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.EventHandlerViolation
        {
            RelativePath = "myfile.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The resolved path is based on the CWD when the event fires
        var expected = Path.GetFullPath(Path.Combine(dir, "myfile.txt"));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void EventHandlerViolation_Unsafe_HandlersAccumulate()
    {
        // Clear static event via reflection
        var eventField = typeof(UnsafeComplex.EventHandlerViolation)
            .GetField("PathResolved", BindingFlags.NonPublic | BindingFlags.Static);

        var task1 = new UnsafeComplex.EventHandlerViolation
        {
            RelativePath = "file1.txt",
            BuildEngine = new MockBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.EventHandlerViolation
        {
            RelativePath = "file2.txt",
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();

        // The static event accumulates handlers across invocations
        var eventValue = eventField?.GetValue(null) as Delegate;
        // After two executions, there should be at least 2 handlers accumulated
        Assert.NotNull(eventValue);
        Assert.True(eventValue!.GetInvocationList().Length >= 2);
    }

    #endregion

    #region LazyInitializationViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.LazyInitializationViolation
        {
            ToolName = "mytool.exe",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedToolPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_Unsafe_CombinesToolNameWithCachedPath()
    {
        var task = new UnsafeComplex.LazyInitializationViolation
        {
            ToolName = "compiler.exe",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The result should end with the tool name
        Assert.EndsWith("compiler.exe", task.ResolvedToolPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LazyInitializationViolation_Unsafe_StaticLazyReturnsSameValueForAllCalls()
    {
        var task1 = new UnsafeComplex.LazyInitializationViolation
        {
            ToolName = "tool1.exe",
            BuildEngine = new MockBuildEngine()
        };
        task1.Execute();

        var task2 = new UnsafeComplex.LazyInitializationViolation
        {
            ToolName = "tool2.exe",
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();

        // Both use the same base directory (from Lazy<T>), only tool name differs
        var baseDir1 = Path.GetDirectoryName(task1.ResolvedToolPath);
        var baseDir2 = Path.GetDirectoryName(task2.ResolvedToolPath);
        Assert.Equal(baseDir1, baseDir2);
    }

    #endregion

    #region LinqPipelineViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.LinqPipelineViolation
        {
            RelativePaths = new[] { "file1.txt", "file2.txt" },
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_Unsafe_ResolvesRelativePathsAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.LinqPipelineViolation
        {
            RelativePaths = new[] { "src", "bin" },
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // All resolved paths should be rooted in the current CWD
        foreach (var resolved in task.ResolvedPaths)
        {
            Assert.StartsWith(dir, resolved);
        }
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_Unsafe_FiltersBlankPaths()
    {
        var task = new UnsafeComplex.LinqPipelineViolation
        {
            RelativePaths = new[] { "file1.txt", "", "  ", "file2.txt" },
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Equal(2, task.ResolvedPaths.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void LinqPipelineViolation_Unsafe_DeduplicatesPaths()
    {
        var task = new UnsafeComplex.LinqPipelineViolation
        {
            RelativePaths = new[] { "same.txt", "same.txt" },
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.Single(task.ResolvedPaths);
    }

    #endregion

    #region NuGetPackageValidator

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void NuGetPackageValidator_Unsafe_ExecuteReturnsTrueWhenFileNotFound()
    {
        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "nonexistent.nuspec",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void NuGetPackageValidator_Unsafe_ValidatesMatchingPackageId()
    {
        var dir = CreateTempDir();
        var nuspecPath = Path.Combine(dir, "test.nuspec");
        var nuspecContent = new XDocument(
            new XElement("package",
                new XElement("metadata",
                    new XElement("id", "TestPackage"),
                    new XElement("version", "1.0.0"))));
        nuspecContent.Save(nuspecPath);

        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = nuspecPath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.True(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void NuGetPackageValidator_Unsafe_InvalidWhenIdMismatch()
    {
        var dir = CreateTempDir();
        var nuspecPath = Path.Combine(dir, "test.nuspec");
        var nuspecContent = new XDocument(
            new XElement("package",
                new XElement("metadata",
                    new XElement("id", "WrongPackage"),
                    new XElement("version", "1.0.0"))));
        nuspecContent.Save(nuspecPath);

        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = nuspecPath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void NuGetPackageValidator_Unsafe_RelativePathDependsOnCwd()
    {
        var dir = CreateTempDir();
        var nuspecPath = "test.nuspec";
        var nuspecContent = new XDocument(
            new XElement("package",
                new XElement("metadata",
                    new XElement("id", "TestPackage"),
                    new XElement("version", "1.0.0"))));
        nuspecContent.Save(Path.Combine(dir, nuspecPath));

        // With CWD set to the directory containing the nuspec, File.Exists finds it
        Environment.CurrentDirectory = dir;
        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = nuspecPath,
            BuildEngine = new MockBuildEngine()
        };
        task.Execute();
        Assert.True(task.IsValid);

        // With CWD changed, File.Exists won't find the relative path
        var otherDir = CreateTempDir();
        Environment.CurrentDirectory = otherDir;
        var task2 = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = nuspecPath,
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();
        Assert.False(task2.IsValid);
    }

    #endregion

    #region ProjectFileAnalyzer

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ProjectFileAnalyzer_Unsafe_ReturnsFalseWhenFileNotFound()
    {
        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "nonexistent.csproj",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.False(result);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ProjectFileAnalyzer_Unsafe_ExtractsPackageReferences()
    {
        var dir = CreateTempDir();
        var projPath = Path.Combine(dir, "test.csproj");
        File.WriteAllText(projPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <PackageReference Include=""xunit"" Version=""2.4.1"" />
  </ItemGroup>
</Project>");

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = projPath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Contains("Newtonsoft.Json", task.PackageReferences);
        Assert.Contains("xunit", task.PackageReferences);
        Assert.Equal(2, task.PackageReferences.Length);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ProjectFileAnalyzer_Unsafe_ProjectReferencesResolveAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;
        var projPath = Path.Combine(dir, "test.csproj");
        File.WriteAllText(projPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Other\Other.csproj"" />
  </ItemGroup>
</Project>");

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = projPath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Single(task.ProjectReferences);
        // BUG: The project reference is resolved via Path.GetFullPath against CWD
        var resolved = task.ProjectReferences[0];
        Assert.True(Path.IsPathRooted(resolved));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ProjectFileAnalyzer_Unsafe_EmptyProjectHasNoReferences()
    {
        var dir = CreateTempDir();
        var projPath = Path.Combine(dir, "empty.csproj");
        File.WriteAllText(projPath, @"<Project Sdk=""Microsoft.NET.Sdk"">
</Project>");

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = projPath,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Empty(task.PackageReferences);
        Assert.Empty(task.ProjectReferences);
    }

    #endregion

    #region ThreadPoolViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ThreadPoolViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedFilePath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ThreadPoolViolation_Unsafe_ResolvesUsingCwdOnPoolThread()
    {
        var dir = CreateTempDir();
        var fileName = "pooltest.txt";
        File.WriteAllText(Path.Combine(dir, fileName), "content");
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = fileName,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The resolved path is CWD-based
        Assert.Contains(dir, task.ResolvedFilePath);
        Assert.True(task.FileFound);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void ThreadPoolViolation_Unsafe_FileNotFoundWhenCwdDiffers()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        File.WriteAllText(Path.Combine(dir1, "test.txt"), "content");

        // CWD is dir2, but the file is in dir1
        Environment.CurrentDirectory = dir2;

        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "test.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        Assert.False(task.FileFound);
    }

    #endregion

    #region UtilityClassViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void UtilityClassViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "mypath",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.AbsolutePath));
        Assert.False(string.IsNullOrEmpty(task.NormalizedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void UtilityClassViolation_Unsafe_RelativePathResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "output",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The resolved path should be under the CWD
        Assert.StartsWith(dir, task.AbsolutePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void UtilityClassViolation_Unsafe_AbsolutePathPassedThrough()
    {
        var absoluteInput = Path.Combine(Path.GetTempPath(), "absolute_test");

        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = absoluteInput,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Absolute paths are passed through unchanged
        Assert.Equal(absoluteInput, task.AbsolutePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void UtilityClassViolation_Unsafe_NormalizedPathHasConsistentSeparators()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "sub/dir/file.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // NormalizedPath should use the platform directory separator
        Assert.DoesNotContain("/", task.NormalizedPath.Replace("://", ""));
    }

    #endregion

    #region AsyncDelegateViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.Result));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AsyncDelegateViolation_Unsafe_UsesCwdAtDelegateExecutionTime()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.AsyncDelegateViolation
        {
            RelativePath = "deferred.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The result is based on CWD at delegate execution time
        Assert.Contains("deferred.txt", task.Result);
    }

    #endregion

    #region BaseClassHidesViolation

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.ResolvedPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Unsafe_ResolvesViaBaseClassAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = "hidden.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The base class uses Path.GetFullPath which resolves against CWD
        var expected = Path.GetFullPath(Path.Combine(dir, "hidden.txt"));
        Assert.Equal(expected, task.ResolvedPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void BaseClassHidesViolation_Unsafe_AbsolutePathPassedThrough()
    {
        var absoluteInput = Path.Combine(Path.GetTempPath(), "abs_test.txt");

        var task = new UnsafeComplex.BaseClassHidesViolation
        {
            InputPath = absoluteInput,
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // Path.GetFullPath returns the absolute path unchanged
        Assert.Equal(absoluteInput, task.ResolvedPath);
    }

    #endregion

    #region DeepCallChainPathResolve

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Unsafe_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "somefile.txt",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.False(string.IsNullOrEmpty(task.OutputPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Unsafe_ResolvesAgainstCwdViaCallChain()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "deep.txt",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, "deep.txt"));
        Assert.Equal(expected, task.OutputPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void DeepCallChainPathResolve_Unsafe_EmptyInputReturnsEmptyPath()
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
    public void DeepCallChainPathResolve_Unsafe_TrimsInputBeforeResolving()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.DeepCallChainPathResolve
        {
            InputPath = "  trimmed.txt  ",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        var expected = Path.GetFullPath(Path.Combine(dir, "trimmed.txt"));
        Assert.Equal(expected, task.OutputPath);
    }

    #endregion

    #region AssemblyReferenceResolver

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Unsafe_ExecuteReturnsTrue()
    {
        // Clear static cache via reflection
        var cacheField = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetField("ResolvedAssemblyCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as Dictionary<string, string>;
        cache?.Clear();

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "System.Core" },
            ReferencePath = "lib",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Single(task.ResolvedPaths);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Unsafe_ResolvesAgainstCwd()
    {
        var cacheField = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetField("ResolvedAssemblyCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as Dictionary<string, string>;
        cache?.Clear();

        var dir = CreateTempDir();
        var libDir = Path.Combine(dir, "lib");
        Directory.CreateDirectory(libDir);
        File.WriteAllText(Path.Combine(libDir, "MyAssembly.dll"), "fake");

        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "MyAssembly" },
            ReferencePath = "lib",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // File.Exists resolves against CWD, so it should find the assembly
        Assert.NotEmpty(task.ResolvedPaths[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    public void AssemblyReferenceResolver_Unsafe_StaticCacheServesStaleResults()
    {
        var cacheField = typeof(UnsafeComplex.AssemblyReferenceResolver)
            .GetField("ResolvedAssemblyCache", BindingFlags.NonPublic | BindingFlags.Static);
        var cache = cacheField?.GetValue(null) as Dictionary<string, string>;
        cache?.Clear();

        var dir1 = CreateTempDir();
        var lib1 = Path.Combine(dir1, "lib");
        Directory.CreateDirectory(lib1);
        File.WriteAllText(Path.Combine(lib1, "Shared.dll"), "v1");

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "Shared" },
            ReferencePath = "lib",
            BuildEngine = new MockBuildEngine()
        };
        task1.Execute();
        var firstResult = task1.ResolvedPaths[0];

        // Change CWD — the static cache will still serve the first result
        var dir2 = CreateTempDir();
        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.AssemblyReferenceResolver
        {
            AssemblyNames = new[] { "Shared" },
            ReferencePath = "lib",
            BuildEngine = new MockBuildEngine()
        };
        task2.Execute();

        // BUG: Static cache serves the result from dir1 even though we're in dir2
        Assert.Equal(firstResult, task2.ResolvedPaths[0]);
    }

    #endregion

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

/// <summary>
/// Tests for ComplexViolation unsafe tasks (batch 3):
/// AssemblyReferenceResolver, AsyncDelegateViolation, BaseClassHidesViolation, DeepCallChainPathResolve.
/// </summary>
public class ComplexViolationBatch3Tests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _savedCwd;

    public ComplexViolationBatch3Tests()
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