using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;

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
}


