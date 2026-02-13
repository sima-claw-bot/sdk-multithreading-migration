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
/// Tests for fixed ComplexViolation tasks (batch 3):
/// TaskAlpha09, TaskAlpha10, TaskAlpha11, TaskAlpha12.
/// </summary>
public class FixedComplexViolationBatch3Tests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fcvb3test_{Guid.NewGuid():N}");
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

    #region TaskAlpha09 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_ExecuteReturnsTrue_WhenFileNotFound()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "nonexistent.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_ValidatesMatchingPackageId()
    {
        var projDir = CreateTempDir();
        var nuspecPath = Path.Combine(projDir, "test.nuspec");
        File.WriteAllText(nuspecPath,
            "<package><metadata><id>TestPackage</id><version>1.0.0</version></metadata></package>");

        var task = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "test.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.True(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_ResolvesNuspecAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var nuspecPath = Path.Combine(projDir, "my.nuspec");
        File.WriteAllText(nuspecPath,
            "<package><metadata><id>Pkg</id><version>1.0.0</version></metadata></package>");

        var task = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "my.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.True(task.IsValid);
        Assert.StartsWith(projDir, task.ResolvedNuspecPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.ResolvedNuspecPath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_InvalidWhenIdMismatch()
    {
        var projDir = CreateTempDir();
        var nuspecPath = Path.Combine(projDir, "mismatch.nuspec");
        File.WriteAllText(nuspecPath,
            "<package><metadata><id>OtherPackage</id><version>1.0.0</version></metadata></package>");

        var task = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            PackageId = "TestPackage",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "mismatch.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.False(task.IsValid);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_DifferentProjectDirs_ResolveDifferently()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        File.WriteAllText(Path.Combine(dir1, "pkg.nuspec"),
            "<package><metadata><id>Pkg</id></metadata></package>");
        File.WriteAllText(Path.Combine(dir2, "pkg.nuspec"),
            "<package><metadata><id>Pkg</id></metadata></package>");

        var task1 = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "pkg.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha09
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            PackageId = "Pkg",
            PackageVersion = "1.0.0",
            NuspecRelativePath = "pkg.nuspec",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedNuspecPath, task2.ResolvedNuspecPath);
        Assert.StartsWith(dir1, task1.ResolvedNuspecPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedNuspecPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha09)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void NuGetPackageValidator_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha09)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    #endregion

    #region TaskAlpha10 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_ReturnsFalseWhenFileNotFound()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ProjectFilePath = "nonexistent.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.False(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_ExtractsPackageReferences()
    {
        var projDir = CreateTempDir();
        var csproj = Path.Combine(projDir, "test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <PackageReference Include=""xunit"" Version=""2.5.0"" />
  </ItemGroup>
</Project>");

        var task = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ProjectFilePath = "test.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Equal(2, task.PackageReferences.Length);
        Assert.Contains("Newtonsoft.Json", task.PackageReferences);
        Assert.Contains("xunit", task.PackageReferences);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_ProjectReferencesResolveAgainstProjectDir()
    {
        var projDir = CreateTempDir();
        var csproj = Path.Combine(projDir, "test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Other\Other.csproj"" />
  </ItemGroup>
</Project>");

        var task = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ProjectFilePath = "test.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Single(task.ProjectReferences);
        Assert.True(Path.IsPathRooted(task.ProjectReferences[0]));
        Assert.Contains("Other.csproj", task.ProjectReferences[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_DifferentProjectDirs_ResolveDifferently()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        // Use a relative reference that stays within the project dir tree
        var content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";
        File.WriteAllText(Path.Combine(dir1, "a.csproj"), content);
        File.WriteAllText(Path.Combine(dir2, "a.csproj"), content);

        var task1 = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            ProjectFilePath = "a.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            ProjectFilePath = "a.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ProjectReferences[0], task2.ProjectReferences[0]);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_EmptyProjectHasNoReferences()
    {
        var projDir = CreateTempDir();
        File.WriteAllText(Path.Combine(projDir, "empty.csproj"),
            @"<Project Sdk=""Microsoft.NET.Sdk""></Project>");

        var task = new FixedComplex.TaskAlpha10
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            ProjectFilePath = "empty.csproj",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
        Assert.Empty(task.PackageReferences);
        Assert.Empty(task.ProjectReferences);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha10)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ProjectFileAnalyzer_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha10)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    #endregion

    #region TaskAlpha11 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativeFilePath = "somefile.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativeFilePath = "subdir\\file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.ResolvedFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.ResolvedFilePath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_FileFoundWhenExists()
    {
        var projDir = CreateTempDir();
        File.WriteAllText(Path.Combine(projDir, "exists.txt"), "content");

        var task = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativeFilePath = "exists.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.True(task.FileFound);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_FileNotFoundWhenMissing()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            RelativeFilePath = "missing.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.False(task.FileFound);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            RelativeFilePath = "file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha11
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            RelativeFilePath = "file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.ResolvedFilePath, task2.ResolvedFilePath);
        Assert.StartsWith(dir1, task1.ResolvedFilePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.ResolvedFilePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha11)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha11)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void ThreadPoolViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha11
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                RelativeFilePath = "file.txt",
                BuildEngine = new Batch3TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.ResolvedFilePath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha11
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                RelativeFilePath = "file.txt",
                BuildEngine = new Batch3TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.ResolvedFilePath;
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

    #region TaskAlpha12 — Fixed tests

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_ExecuteReturnsTrue()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "somefile.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        Assert.True(task.Execute());
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_ResolvesAgainstProjectDirectory()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "subdir\\file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.StartsWith(projDir, task.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(task.AbsolutePath));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_AbsolutePathPassedThrough()
    {
        var projDir = CreateTempDir();
        var absoluteInput = Path.Combine(Path.GetTempPath(), "abs_test.txt");
        var task = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = absoluteInput,
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.Equal(absoluteInput, task.AbsolutePath);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_NormalizedPathHasConsistentSeparators()
    {
        var projDir = CreateTempDir();
        var task = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = "sub/dir/file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task.Execute();

        Assert.DoesNotContain("/", task.NormalizedPath.Replace("://", ""));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_DifferentProjectDirs_ProduceDifferentResults()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        var task1 = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
            InputPath = "file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task1.Execute();

        var task2 = new FixedComplex.TaskAlpha12
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
            InputPath = "file.txt",
            BuildEngine = new Batch3TestBuildEngine()
        };
        task2.Execute();

        Assert.NotEqual(task1.AbsolutePath, task2.AbsolutePath);
        Assert.StartsWith(dir1, task1.AbsolutePath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, task2.AbsolutePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_ImplementsIMultiThreadableTask()
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedComplex.TaskAlpha12)));
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = typeof(FixedComplex.TaskAlpha12)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ComplexViolation")]
    [Trait("Target", "Fixed")]
    public void UtilityClassViolation_Fixed_ConcurrentEachUsesOwnProjectDir()
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha12
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir1 },
                InputPath = "file.txt",
                BuildEngine = new Batch3TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result1 = task.AbsolutePath;
        });

        var t2 = new Thread(() =>
        {
            var task = new FixedComplex.TaskAlpha12
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir2 },
                InputPath = "file.txt",
                BuildEngine = new Batch3TestBuildEngine()
            };
            barrier.SignalAndWait();
            task.Execute();
            result2 = task.AbsolutePath;
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
/// Minimal IBuildEngine implementation for batch 3 tests.
/// </summary>
internal class Batch3TestBuildEngine : Microsoft.Build.Framework.IBuildEngine
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
