#nullable enable
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using UnsafeThreadSafeTasks.ComplexViolations;
using Xunit;

using UnsafeComplex = UnsafeThreadSafeTasks.ComplexViolations;

namespace UnsafeThreadSafeTasks.Tests;

public class ComplexViolationTests : IDisposable
{
    private readonly ConcurrentBag<string> _tempDirs = new();
    private readonly string _tempDir;
    private readonly string _savedCwd;

    public ComplexViolationTests()
    {
        _savedCwd = Directory.GetCurrentDirectory();
        _tempDir = CreateTempDir();
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
        Directory.SetCurrentDirectory(_savedCwd);
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    private static ComplexMockBuildEngine NewEngine() => new();
    #region BaseClassHidesViolation

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.BaseClassHidesViolation))]
    public void BaseClassHidesViolation_ResolvesRelativePathAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task, "subdir");

        Assert.True(task.Execute());

        var resolved = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task)!;
        Assert.StartsWith(dir, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.BaseClassHidesViolation))]
    public void BaseClassHidesViolation_TwoProjectDirs_BothGetCwdNotOwnDir(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var barrier = new Barrier(2);
        string? result1 = null, result2 = null;

        var t1 = new Thread(() =>
        {
            var task = (Task)Activator.CreateInstance(taskType)!;
            task.BuildEngine = NewEngine();
            taskType.GetProperty("InputPath")!.SetValue(task, "src");
            barrier.SignalAndWait();
            Directory.SetCurrentDirectory(dir1);
            task.Execute();
            result1 = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task)!;
        });

        var t2 = new Thread(() =>
        {
            var task = (Task)Activator.CreateInstance(taskType)!;
            task.BuildEngine = NewEngine();
            taskType.GetProperty("InputPath")!.SetValue(task, "src");
            barrier.SignalAndWait();
            Directory.SetCurrentDirectory(dir2);
            task.Execute();
            result2 = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task)!;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Both tasks resolve "src" ΓÇö but against whichever CWD is active, so they
        // may both resolve the same way (race condition).
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        // Both results should be absolute paths
        Assert.True(Path.IsPathRooted(result1!));
        Assert.True(Path.IsPathRooted(result2!));
    }

    #endregion

    #region DeepCallChainPathResolve

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.DeepCallChainPathResolve))]
    public void DeepCallChainPathResolve_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task, "output");

        Assert.True(task.Execute());

        var output = (string)taskType.GetProperty("OutputPath")!.GetValue(task)!;
        Assert.StartsWith(dir, output, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.DeepCallChainPathResolve))]
    public void DeepCallChainPathResolve_DifferentCwd_ProducesDifferentResult(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task1, "build");
        task1.Execute();
        var result1 = (string)taskType.GetProperty("OutputPath")!.GetValue(task1)!;

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task2, "build");
        task2.Execute();
        var result2 = (string)taskType.GetProperty("OutputPath")!.GetValue(task2)!;

        // Same relative path produces different absolute paths because CWD differs
        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region UtilityClassViolation

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.UtilityClassViolation))]
    public void UtilityClassViolation_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task, "lib");

        Assert.True(task.Execute());

        var abs = (string)taskType.GetProperty("AbsolutePath")!.GetValue(task)!;
        var norm = (string)taskType.GetProperty("NormalizedPath")!.GetValue(task)!;
        Assert.StartsWith(dir, abs, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir, norm, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.UtilityClassViolation))]
    public void UtilityClassViolation_DifferentCwd_ProducesDifferentResult(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task1, "pkg");
        task1.Execute();
        var result1 = (string)taskType.GetProperty("AbsolutePath")!.GetValue(task1)!;

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("InputPath")!.SetValue(task2, "pkg");
        task2.Execute();
        var result2 = (string)taskType.GetProperty("AbsolutePath")!.GetValue(task2)!;

        Assert.NotEqual(result1, result2);
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(dir2, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region LinqPipelineViolation

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.LinqPipelineViolation))]
    public void LinqPipelineViolation_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePaths")!.SetValue(task, new[] { "src", "tests" });

        Assert.True(task.Execute());

        var resolved = (string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task)!;
        Assert.Equal(2, resolved.Length);
        Assert.All(resolved, r => Assert.StartsWith(dir, r, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.LinqPipelineViolation))]
    public void LinqPipelineViolation_DifferentCwd_ProducesDifferentResults(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePaths")!.SetValue(task1, new[] { "bin" });
        task1.Execute();
        var result1 = ((string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task1)!)[0];

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePaths")!.SetValue(task2, new[] { "bin" });
        task2.Execute();
        var result2 = ((string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task2)!)[0];

        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region AsyncDelegateViolation

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.AsyncDelegateViolation))]
    public void AsyncDelegateViolation_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task, "data");

        Assert.True(task.Execute());

        var result = (string)taskType.GetProperty("Result")!.GetValue(task)!;
        // The result is a combined path using CWD
        Assert.Contains("data", result);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.AsyncDelegateViolation))]
    public void AsyncDelegateViolation_DifferentCwd_ProducesDifferentResult(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task1, "file.txt");
        task1.Execute();
        var result1 = (string)taskType.GetProperty("Result")!.GetValue(task1)!;

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task2, "file.txt");
        task2.Execute();
        var result2 = (string)taskType.GetProperty("Result")!.GetValue(task2)!;

        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region ThreadPoolViolation

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.ThreadPoolViolation))]
    public void ThreadPoolViolation_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        var testFile = "test.txt";
        File.WriteAllText(Path.Combine(dir, testFile), "content");
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("RelativeFilePath")!.SetValue(task, testFile);

        Assert.True(task.Execute());

        var resolved = (string)taskType.GetProperty("ResolvedFilePath")!.GetValue(task)!;
        var found = (bool)taskType.GetProperty("FileFound")!.GetValue(task)!;
        Assert.True(found);
        Assert.Contains(testFile, resolved);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.ThreadPoolViolation))]
    public void ThreadPoolViolation_DifferentCwd_ResolvesToDifferentPaths(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("RelativeFilePath")!.SetValue(task1, "app.dll");
        task1.Execute();
        var result1 = (string)taskType.GetProperty("ResolvedFilePath")!.GetValue(task1)!;

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("RelativeFilePath")!.SetValue(task2, "app.dll");
        task2.Execute();
        var result2 = (string)taskType.GetProperty("ResolvedFilePath")!.GetValue(task2)!;

        Assert.NotEqual(result1, result2);
    }

    #endregion

    #region DictionaryCacheViolation ΓÇö static cross-contamination

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.DictionaryCacheViolation))]
    public void DictionaryCacheViolation_StaticCache_CrossContaminates(Type taskType)
    {
        // Use a unique relative path per test invocation to avoid stale cache
        // from other tests in the same process.
        var uniqueKey = $"cached_{Guid.NewGuid():N}";
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        // First task: resolve under dir1
        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task1, uniqueKey);
        task1.Execute();
        var result1 = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task1)!;

        // Second task: same relative path, different CWD ΓåÆ should get different result
        // but the static cache serves the stale value from dir1
        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task2, uniqueKey);
        task2.Execute();
        var result2 = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task2)!;

        // BUG: both return the same resolved path ΓÇö the cache serves the stale entry
        Assert.Equal(result1, result2);
        Assert.StartsWith(dir1, result1, StringComparison.OrdinalIgnoreCase);
        // The second result should have been under dir2, but it's under dir1
        Assert.StartsWith(dir1, result2, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region LazyInitializationViolation ΓÇö static cross-contamination

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.LazyInitializationViolation))]
    public void LazyInitializationViolation_StaticLazy_ReturnsStaleValue(Type taskType)
    {
        // The Lazy<string> resolves "tools" once and caches it forever.
        // Whatever CWD is active at first access locks in the result.
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("ToolName")!.SetValue(task1, "mytool");
        task1.Execute();
        var result1 = (string)taskType.GetProperty("ResolvedToolPath")!.GetValue(task1)!;

        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("ToolName")!.SetValue(task2, "mytool");
        task2.Execute();
        var result2 = (string)taskType.GetProperty("ResolvedToolPath")!.GetValue(task2)!;

        // Both return the same path because the Lazy was initialized once
        Assert.Equal(result1, result2);
    }

    #endregion

    #region EventHandlerViolation ΓÇö static cross-contamination

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.EventHandlerViolation))]
    public void EventHandlerViolation_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task, "output");

        Assert.True(task.Execute());

        var resolved = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task)!;
        Assert.StartsWith(dir, resolved, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.EventHandlerViolation))]
    public void EventHandlerViolation_StaticEventHandlersAccumulate(Type taskType)
    {
        var dir = CreateTempDir();
        Directory.SetCurrentDirectory(dir);

        // Each Execute() adds a new handler to the static event.
        // After multiple invocations, the last handler's result wins, but
        // all handlers fire ΓÇö demonstrating accumulation.
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task1, "first");
        task1.Execute();

        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("RelativePath")!.SetValue(task2, "second");
        task2.Execute();

        var resolved = (string)taskType.GetProperty("ResolvedPath")!.GetValue(task2)!;
        // The result is still valid (resolves against CWD) ΓÇö the violation is the
        // accumulation of handlers and CWD dependency
        Assert.True(Path.IsPathRooted(resolved));
    }

    #endregion

    #region AssemblyReferenceResolver ΓÇö static cache cross-contamination

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.AssemblyReferenceResolver))]
    public void AssemblyReferenceResolver_ResolvesFileExistsAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        var refDir = "refs";
        var fullRefDir = Path.Combine(dir, refDir);
        Directory.CreateDirectory(fullRefDir);
        File.WriteAllText(Path.Combine(fullRefDir, "MyLib.dll"), "fake");

        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("AssemblyNames")!.SetValue(task, new[] { "MyLib" });
        taskType.GetProperty("ReferencePath")!.SetValue(task, refDir);

        Assert.True(task.Execute());

        var paths = (string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task)!;
        Assert.Single(paths);
        Assert.Contains("MyLib.dll", paths[0]);
        Assert.StartsWith(dir, paths[0], StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.AssemblyReferenceResolver))]
    public void AssemblyReferenceResolver_StaticCache_CrossContaminates(Type taskType)
    {
        // Use a unique assembly name to avoid cache interference from other tests
        var asmName = $"Asm_{Guid.NewGuid():N}";
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();

        // Create assembly file only in dir1
        var refDir = "refs";
        Directory.CreateDirectory(Path.Combine(dir1, refDir));
        File.WriteAllText(Path.Combine(dir1, refDir, asmName + ".dll"), "fake");
        // Intentionally do NOT create the file in dir2

        // First: resolve from dir1 ΓÇö finds the file
        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("AssemblyNames")!.SetValue(task1, new[] { asmName });
        taskType.GetProperty("ReferencePath")!.SetValue(task1, refDir);
        task1.Execute();
        var paths1 = (string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task1)!;

        // Second: resolve from dir2 ΓÇö file doesn't exist here, but the static cache
        // returns the stale entry from dir1
        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("AssemblyNames")!.SetValue(task2, new[] { asmName });
        taskType.GetProperty("ReferencePath")!.SetValue(task2, refDir);
        task2.Execute();
        var paths2 = (string[])taskType.GetProperty("ResolvedPaths")!.GetValue(task2)!;

        // BUG: static cache serves the path resolved under dir1 to dir2's task
        Assert.Equal(paths1[0], paths2[0]);
        Assert.StartsWith(dir1, paths2[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region ProjectFileAnalyzer

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.ProjectFileAnalyzer))]
    public void ProjectFileAnalyzer_ParsesProjectFile(Type taskType)
    {
        var dir = CreateTempDir();
        var csproj = Path.Combine(dir, "Test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <ProjectReference Include=""..\Other\Other.csproj"" />
  </ItemGroup>
</Project>");

        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("ProjectFilePath")!.SetValue(task, csproj);

        Assert.True(task.Execute());

        var pkgRefs = (string[])taskType.GetProperty("PackageReferences")!.GetValue(task)!;
        var projRefs = (string[])taskType.GetProperty("ProjectReferences")!.GetValue(task)!;

        Assert.Single(pkgRefs);
        Assert.Equal("Newtonsoft.Json", pkgRefs[0]);
        Assert.Single(projRefs);
        // Project reference path resolved via Path.GetFullPath against CWD
        Assert.True(Path.IsPathRooted(projRefs[0]));
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.ProjectFileAnalyzer))]
    public void ProjectFileAnalyzer_ProjectRefResolvesAgainstCwd(Type taskType)
    {
        var dir1 = CreateTempDir();
        // Create a nested directory so CWD differs at a deeper level
        var dir2 = Path.Combine(CreateTempDir(), "nested");
        Directory.CreateDirectory(dir2);

        var csproj = Path.Combine(dir1, "Test.csproj");
        File.WriteAllText(csproj, @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""sub\Other.csproj"" />
  </ItemGroup>
</Project>");

        // Run with CWD = dir1
        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("ProjectFilePath")!.SetValue(task1, csproj);
        task1.Execute();
        var refs1 = (string[])taskType.GetProperty("ProjectReferences")!.GetValue(task1)!;

        // Run with CWD = dir2 ΓÇö same file, but relative ref resolves differently
        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("ProjectFilePath")!.SetValue(task2, csproj);
        task2.Execute();
        var refs2 = (string[])taskType.GetProperty("ProjectReferences")!.GetValue(task2)!;

        // The relative "sub\Other.csproj" resolves against CWD, not the project dir
        Assert.NotEqual(refs1[0], refs2[0]);
    }

    #endregion

    #region NuGetPackageValidator

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.NuGetPackageValidator))]
    public void NuGetPackageValidator_ValidNuspec_ResolvesAgainstCwd(Type taskType)
    {
        var dir = CreateTempDir();
        var nuspecPath = "pkg.nuspec";
        File.WriteAllText(Path.Combine(dir, nuspecPath), @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>TestPkg</id>
    <version>1.0.0</version>
  </metadata>
</package>");

        Directory.SetCurrentDirectory(dir);

        var task = (Task)Activator.CreateInstance(taskType)!;
        task.BuildEngine = NewEngine();
        taskType.GetProperty("PackageId")!.SetValue(task, "TestPkg");
        taskType.GetProperty("PackageVersion")!.SetValue(task, "1.0.0");
        taskType.GetProperty("NuspecRelativePath")!.SetValue(task, nuspecPath);

        Assert.True(task.Execute());

        var isValid = (bool)taskType.GetProperty("IsValid")!.GetValue(task)!;
        var resolvedPath = (string)taskType.GetProperty("ResolvedNuspecPath")!.GetValue(task)!;
        Assert.True(isValid);
        Assert.StartsWith(dir, resolvedPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [Trait("Category", "ComplexViolation")]
    [InlineData(typeof(UnsafeComplex.NuGetPackageValidator))]
    public void NuGetPackageValidator_FileNotFoundUnderDifferentCwd(Type taskType)
    {
        var dir1 = CreateTempDir();
        var dir2 = CreateTempDir();
        var nuspecPath = "pkg.nuspec";

        // Put nuspec only in dir1
        File.WriteAllText(Path.Combine(dir1, nuspecPath), @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>TestPkg</id>
    <version>1.0.0</version>
  </metadata>
</package>");

        // CWD = dir1 ΓåÆ file found
        Directory.SetCurrentDirectory(dir1);
        var task1 = (Task)Activator.CreateInstance(taskType)!;
        task1.BuildEngine = NewEngine();
        taskType.GetProperty("PackageId")!.SetValue(task1, "TestPkg");
        taskType.GetProperty("PackageVersion")!.SetValue(task1, "1.0.0");
        taskType.GetProperty("NuspecRelativePath")!.SetValue(task1, nuspecPath);
        task1.Execute();
        var valid1 = (bool)taskType.GetProperty("IsValid")!.GetValue(task1)!;

        // CWD = dir2 ΓåÆ file not found
        Directory.SetCurrentDirectory(dir2);
        var task2 = (Task)Activator.CreateInstance(taskType)!;
        task2.BuildEngine = NewEngine();
        taskType.GetProperty("PackageId")!.SetValue(task2, "TestPkg");
        taskType.GetProperty("PackageVersion")!.SetValue(task2, "1.0.0");
        taskType.GetProperty("NuspecRelativePath")!.SetValue(task2, nuspecPath);
        task2.Execute();
        var valid2 = (bool)taskType.GetProperty("IsValid")!.GetValue(task2)!;

        Assert.True(valid1);
        Assert.False(valid2);
    }

    #endregion

    #region AssemblyReferenceResolver (direct instantiation)

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

    #region AsyncDelegateViolation (direct instantiation)

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

    #region BaseClassHidesViolation (direct instantiation)

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

        // Different CWDs produce different results ΓÇö violation hidden in base class
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

    #region DeepCallChainPathResolve (direct instantiation)

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

        // Different CWDs produce different results ΓÇö violation 3 levels deep
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

internal class ComplexMockBuildEngine : IBuildEngine
{
    public List<BuildErrorEventArgs> Errors { get; } = new();
    public List<BuildWarningEventArgs> Warnings { get; } = new();
    public List<BuildMessageEventArgs> Messages { get; } = new();

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        IDictionary globalProperties, IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e);
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
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
