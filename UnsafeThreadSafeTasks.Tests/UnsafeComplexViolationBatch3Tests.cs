using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Tests for unsafe ComplexViolation tasks (batch 3):
/// NuGetPackageValidator, ProjectFileAnalyzer, ThreadPoolViolation, UtilityClassViolation.
/// These tests verify the CWD-dependent bugs present in the unsafe versions.
/// </summary>
public class UnsafeComplexViolationBatch3Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _originalCwd;

    public UnsafeComplexViolationBatch3Tests()
    {
        _originalCwd = Environment.CurrentDirectory;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ucvb3test_{Guid.NewGuid():N}");
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

    #region NuGetPackageValidator — CWD-dependent File.Exists and XDocument.Load

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_ExecuteReturnsTrue_WhenFileNotFound()
    {
        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "nonexistent_" + Guid.NewGuid().ToString("N") + ".nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_ValidatesMatchingPackageId()
    {
        var dir = CreateTempDir();
        var nuspecPath = Path.Combine(dir, "test.nuspec");
        File.WriteAllText(nuspecPath,
            "<package><metadata><id>TestPackage</id><version>1.0.0</version></metadata></package>");

        // BUG: File.Exists uses relative path resolved against CWD
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "test.nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.True(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_ResolvesNuspecAgainstCwd()
    {
        var dir = CreateTempDir();
        var nuspecPath = Path.Combine(dir, "my.nuspec");
        File.WriteAllText(nuspecPath,
            "<package><metadata><id>Pkg</id><version>1.0.0</version></metadata></package>");

        // BUG: Path.GetFullPath resolves against process CWD
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "my.nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        Assert.True(task.IsValid);
        Assert.StartsWith(dir, task.ResolvedNuspecPath);
        Assert.True(Path.IsPathRooted(task.ResolvedNuspecPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_InvalidWhenIdMismatch()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "mismatch.nuspec"),
            "<package><metadata><id>OtherPackage</id><version>1.0.0</version></metadata></package>");

        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "mismatch.nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_CwdChange_AffectsResolution()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        File.WriteAllText(Path.Combine(dir1, "pkg.nuspec"),
            "<package><metadata><id>Pkg</id><version>1.0.0</version></metadata></package>");
        File.WriteAllText(Path.Combine(dir2, "pkg.nuspec"),
            "<package><metadata><id>Pkg</id><version>1.0.0</version></metadata></package>");

        // BUG: CWD determines where the nuspec is found
        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "pkg.nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.NuGetPackageValidator
        {
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "pkg.nuspec",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedNuspecPath, task2.ResolvedNuspecPath);
        Assert.StartsWith(dir1, task1.ResolvedNuspecPath);
        Assert.StartsWith(dir2, task2.ResolvedNuspecPath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.NuGetPackageValidator)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.NuGetPackageValidator)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.NuGetPackageValidator).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void NuGetPackageValidator_DefaultProperties()
    {
        var task = new UnsafeComplex.NuGetPackageValidator();
        Assert.Equal(string.Empty, task.PackageId);
        Assert.Equal(string.Empty, task.PackageVersion);
        Assert.Equal(string.Empty, task.NuspecRelativePath);
        Assert.False(task.IsValid);
        Assert.Equal(string.Empty, task.ResolvedNuspecPath);
    }

    #endregion

    #region ProjectFileAnalyzer — CWD-dependent XDocument.Load and Path.GetFullPath

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_ReturnsFalseWhenFileNotFound()
    {
        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "nonexistent_" + Guid.NewGuid().ToString("N") + ".csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.False(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_ExtractsPackageReferences()
    {
        var dir = CreateTempDir();
        var csproj = Path.Combine(dir, "test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <PackageReference Include=""xunit"" Version=""2.5.0"" />
  </ItemGroup>
</Project>");

        // BUG: File.Exists and XDocument.Load resolve against CWD
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "test.csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Equal(2, task.PackageReferences.Length);
        Assert.Contains("Newtonsoft.Json", task.PackageReferences);
        Assert.Contains("xunit", task.PackageReferences);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_ProjectReferencesResolveAgainstCwd()
    {
        var dir = CreateTempDir();
        var csproj = Path.Combine(dir, "test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Other\Other.csproj"" />
  </ItemGroup>
</Project>");

        // BUG: Path.GetFullPath resolves relative project references against CWD
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "test.csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Single(task.ProjectReferences);
        Assert.True(Path.IsPathRooted(task.ProjectReferences[0]));
        Assert.Contains("Other.csproj", task.ProjectReferences[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_CwdChange_AffectsProjectReferenceResolution()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(Path.Combine(dir1, "a.csproj"), content);
        File.WriteAllText(Path.Combine(dir2, "a.csproj"), content);

        // BUG: Different CWDs produce different resolved project reference paths
        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "a.csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "a.csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ProjectReferences[0], task2.ProjectReferences[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_EmptyProjectHasNoReferences()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "empty.csproj"),
            @"<Project Sdk=""Microsoft.NET.Sdk""></Project>");

        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ProjectFileAnalyzer
        {
            ProjectFilePath = "empty.csproj",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Empty(task.PackageReferences);
        Assert.Empty(task.ProjectReferences);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.ProjectFileAnalyzer)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.ProjectFileAnalyzer)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.ProjectFileAnalyzer).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ProjectFileAnalyzer_DefaultProperties()
    {
        var task = new UnsafeComplex.ProjectFileAnalyzer();
        Assert.Equal(string.Empty, task.ProjectFilePath);
        Assert.Empty(task.PackageReferences);
        Assert.Empty(task.ProjectReferences);
    }

    #endregion

    #region ThreadPoolViolation — CWD captured on thread pool thread

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "somefile.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_ResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "subdir\\file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        // BUG: uses Directory.GetCurrentDirectory() on the thread pool thread
        Assert.StartsWith(dir, task.ResolvedFilePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_FileFoundWhenExists()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, "exists.txt"), "content");

        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "exists.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        Assert.True(task.FileFound);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_FileNotFoundWhenMissing()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "missing_" + Guid.NewGuid().ToString("N") + ".txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        Assert.False(task.FileFound);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.ThreadPoolViolation
        {
            RelativeFilePath = "file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task2.Execute();

        // BUG: Different CWDs produce different resolved paths
        Assert.NotEqual(task1.ResolvedFilePath, task2.ResolvedFilePath);
        Assert.StartsWith(dir1, task1.ResolvedFilePath);
        Assert.StartsWith(dir2, task2.ResolvedFilePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.ThreadPoolViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.ThreadPoolViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.ThreadPoolViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void ThreadPoolViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.ThreadPoolViolation();
        Assert.Equal(string.Empty, task.RelativeFilePath);
        Assert.Equal(string.Empty, task.ResolvedFilePath);
        Assert.False(task.FileFound);
    }

    #endregion

    #region UtilityClassViolation — CWD hidden behind static utility class

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_ExecuteReturnsTrue()
    {
        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "somefile.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_ResolvesAgainstCwd()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "subdir\\file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        // BUG: PathUtilities.MakeAbsolute uses Path.GetFullPath which resolves against CWD
        Assert.StartsWith(dir, task.AbsolutePath);
        Assert.True(Path.IsPathRooted(task.AbsolutePath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_AbsolutePathPassedThrough()
    {
        var absoluteInput = Path.Combine(Path.GetTempPath(), "abs_test.txt");
        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = absoluteInput,
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(absoluteInput, task.AbsolutePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_NormalizedPathHasConsistentSeparators()
    {
        var dir = CreateTempDir();
        Environment.CurrentDirectory = dir;

        var task = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "sub/dir/file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task.Execute();

        // NormalizeSeparators replaces / with platform separator
        Assert.DoesNotContain("/", task.NormalizedPath.Replace("://", ""));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_CwdChange_ProducesDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Environment.CurrentDirectory = dir1;
        var task1 = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task1.Execute();

        Environment.CurrentDirectory = dir2;
        var task2 = new UnsafeComplex.UtilityClassViolation
        {
            InputPath = "file.txt",
            BuildEngine = new UnsafeBatch3TestBuildEngine()
        };
        task2.Execute();

        // BUG: Different CWDs produce different absolute paths
        Assert.NotEqual(task1.AbsolutePath, task2.AbsolutePath);
        Assert.StartsWith(dir1, task1.AbsolutePath);
        Assert.StartsWith(dir2, task2.AbsolutePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_DoesNotImplementIMultiThreadableTask()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeComplex.UtilityClassViolation)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(UnsafeComplex.UtilityClassViolation)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_DoesNotHaveTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeComplex.UtilityClassViolation).GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    public void UtilityClassViolation_DefaultProperties()
    {
        var task = new UnsafeComplex.UtilityClassViolation();
        Assert.Equal(string.Empty, task.InputPath);
        Assert.Equal(string.Empty, task.AbsolutePath);
        Assert.Equal(string.Empty, task.NormalizedPath);
    }

    #endregion

    #region Cross-cutting structural checks for all batch 3 types

    public static IEnumerable<object[]> Batch3TaskTypes()
    {
        yield return new object[] { typeof(UnsafeComplex.NuGetPackageValidator) };
        yield return new object[] { typeof(UnsafeComplex.ProjectFileAnalyzer) };
        yield return new object[] { typeof(UnsafeComplex.ThreadPoolViolation) };
        yield return new object[] { typeof(UnsafeComplex.UtilityClassViolation) };
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_ExtendsTask(Type taskType)
    {
        Assert.True(typeof(MSBuildTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_DoesNotImplementIMultiThreadableTask(Type taskType)
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_IsInCorrectNamespace(Type taskType)
    {
        Assert.Equal("UnsafeThreadSafeTasks.ComplexViolations", taskType.Namespace);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_CanBeInstantiated(Type taskType)
    {
        var instance = Activator.CreateInstance(taskType);
        Assert.NotNull(instance);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Unsafe")]
    [Trait("Batch", "3")]
    [MemberData(nameof(Batch3TaskTypes))]
    public void Batch3Type_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    #endregion
}

/// <summary>
/// Minimal IBuildEngine implementation for UnsafeComplexViolation batch 3 tests.
/// </summary>
internal class UnsafeBatch3TestBuildEngine : IBuildEngine
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
