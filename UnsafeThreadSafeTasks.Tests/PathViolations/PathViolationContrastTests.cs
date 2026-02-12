#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

using Unsafe = UnsafeThreadSafeTasks.PathViolations;
using Fixed = FixedThreadSafeTasks.PathViolations;

namespace UnsafeThreadSafeTasks.Tests.PathViolations;

/// <summary>
/// Contrast tests that directly compare unsafe PathViolation tasks against their
/// fixed counterparts. Each test verifies that the unsafe task resolves paths via
/// process CWD while the fixed task resolves via TaskEnvironment.ProjectDirectory.
/// </summary>
[Trait("Category", "PathViolation")]
[Trait("Target", "Contrast")]
[Collection("CwdSensitive")]
public class PathViolationContrastTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"pvcontrast_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static ContrastBuildEngine Engine() => new();

    #region DirectoryExists: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void RelativePathToDirectoryExists_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var subName = "contrast_subdir";

        // Create subdir only under projectDir, not under cwdDir
        Directory.CreateDirectory(Path.Combine(projectDir, subName));

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            // Unsafe: resolves against CWD → subdir doesn't exist there
            var unsafeTask = new Unsafe.RelativePathToDirectoryExists
            {
                InputPath = subName,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();
            Assert.Equal("False", unsafeTask.Result);

            // Fixed: resolves against ProjectDirectory → subdir exists
            var fixedTask = new Fixed.RelativePathToDirectoryExists
            {
                InputPath = subName,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();
            Assert.Equal("True", fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region FileExists: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void RelativePathToFileExists_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var fileName = "contrast_file.txt";

        // Create file only under projectDir
        File.WriteAllText(Path.Combine(projectDir, fileName), "content");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            var unsafeTask = new Unsafe.RelativePathToFileExists
            {
                InputPath = fileName,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();
            Assert.Equal("False", unsafeTask.Result);

            var fixedTask = new Fixed.RelativePathToFileExists
            {
                InputPath = fileName,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();
            Assert.Equal("True", fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region FileStream: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void RelativePathToFileStream_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var fileName = "contrast_stream.txt";

        File.WriteAllText(Path.Combine(projectDir, fileName), "project content");
        File.WriteAllText(Path.Combine(cwdDir, fileName), "cwd content");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            // Unsafe: reads from CWD
            var unsafeTask = new Unsafe.RelativePathToFileStream
            {
                InputPath = fileName,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();
            Assert.Equal("cwd content", unsafeTask.Result);

            // Fixed: reads from ProjectDirectory
            var fixedTask = new Fixed.RelativePathToFileStream
            {
                InputPath = fileName,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();
            Assert.Equal("project content", fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region XDocument: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void RelativePathToXDocument_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var fileName = "contrast.xml";

        File.WriteAllText(Path.Combine(cwdDir, fileName), "<CwdRoot />");
        File.WriteAllText(Path.Combine(projectDir, fileName), "<ProjectRoot />");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            var unsafeTask = new Unsafe.RelativePathToXDocument
            {
                InputPath = fileName,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();
            Assert.Equal("CwdRoot", unsafeTask.Result);

            var fixedTask = new Fixed.RelativePathToXDocument
            {
                InputPath = fileName,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();
            Assert.Equal("ProjectRoot", fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region GetFullPath_AttributeOnly: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void UsesPathGetFullPath_AttributeOnly_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var relPath = "sub/file.txt";

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            var unsafeTask = new Unsafe.UsesPathGetFullPath_AttributeOnly
            {
                InputPath = relPath,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            var fixedTask = new Fixed.UsesPathGetFullPath_AttributeOnly
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            // Unsafe resolves against CWD, fixed resolves against ProjectDirectory
            Assert.StartsWith(cwdDir, unsafeTask.Result, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(projectDir, fixedTask.Result, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(unsafeTask.Result, fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region GetFullPath_ForCanonicalization: unsafe CWD vs fixed ProjectDir

    [Fact]
    public void UsesPathGetFullPath_ForCanonicalization_UnsafeVsFixed_DifferentResolutionBase()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var relPath = Path.Combine("a", "..", "file.txt");

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            var unsafeTask = new Unsafe.UsesPathGetFullPath_ForCanonicalization
            {
                InputPath = relPath,
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            var fixedTask = new Fixed.UsesPathGetFullPath_ForCanonicalization
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            // Both canonicalize, but from different bases
            Assert.DoesNotContain("..", unsafeTask.Result);
            Assert.DoesNotContain("..", fixedTask.Result);
            Assert.StartsWith(cwdDir, unsafeTask.Result, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(projectDir, fixedTask.Result, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(unsafeTask.Result, fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region GetFullPath_IgnoresTaskEnv: unsafe ignores TaskEnv vs fixed uses it

    [Fact]
    public void UsesPathGetFullPath_IgnoresTaskEnv_UnsafeVsFixed_TaskEnvUsage()
    {
        var cwdDir = CreateTempDir();
        var projectDir = CreateTempDir();
        var relPath = "file.txt";

        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(cwdDir);

            // Unsafe: implements IMultiThreadableTask but ignores TaskEnvironment
            var unsafeTask = new Unsafe.UsesPathGetFullPath_IgnoresTaskEnv
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            // Fixed: properly uses TaskEnvironment.ProjectDirectory
            var fixedTask = new Fixed.UsesPathGetFullPath_IgnoresTaskEnv
            {
                InputPath = relPath,
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir },
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            // Unsafe resolves against CWD despite having TaskEnvironment
            Assert.StartsWith(cwdDir, unsafeTask.Result, StringComparison.OrdinalIgnoreCase);
            // Fixed resolves against ProjectDirectory
            Assert.StartsWith(projectDir, fixedTask.Result, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(unsafeTask.Result, fixedTask.Result);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
        }
    }

    #endregion

    #region Structural: all 7 unsafe tasks lack IMultiThreadableTask (except IgnoresTaskEnv)

    [Theory]
    [InlineData(typeof(Unsafe.RelativePathToDirectoryExists))]
    [InlineData(typeof(Unsafe.RelativePathToFileExists))]
    [InlineData(typeof(Unsafe.RelativePathToFileStream))]
    [InlineData(typeof(Unsafe.RelativePathToXDocument))]
    [InlineData(typeof(Unsafe.UsesPathGetFullPath_AttributeOnly))]
    [InlineData(typeof(Unsafe.UsesPathGetFullPath_ForCanonicalization))]
    public void UnsafePathTasks_WithoutIMultiThreadableTask_CannotReceiveProjectDir(Type unsafeType)
    {
        // These 6 unsafe tasks don't implement IMultiThreadableTask,
        // so they have no way to receive a per-task project directory
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(unsafeType));
        Assert.Null(unsafeType.GetProperty("TaskEnvironment"));
    }

    [Theory]
    [InlineData(typeof(Fixed.RelativePathToDirectoryExists))]
    [InlineData(typeof(Fixed.RelativePathToFileExists))]
    [InlineData(typeof(Fixed.RelativePathToFileStream))]
    [InlineData(typeof(Fixed.RelativePathToXDocument))]
    [InlineData(typeof(Fixed.UsesPathGetFullPath_AttributeOnly))]
    [InlineData(typeof(Fixed.UsesPathGetFullPath_ForCanonicalization))]
    [InlineData(typeof(Fixed.UsesPathGetFullPath_IgnoresTaskEnv))]
    public void FixedPathTasks_AllImplementIMultiThreadableTask(Type fixedType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(fixedType));
        Assert.NotNull(fixedType.GetProperty("TaskEnvironment"));
    }

    #endregion

    #region Absolute paths: both unsafe and fixed produce same result

    [Fact]
    public void AllPathTasks_WithAbsolutePath_UnsafeAndFixedAgree()
    {
        var projectDir = CreateTempDir();
        var absFile = Path.Combine(projectDir, "abs_test.txt");
        File.WriteAllText(absFile, "absolute content");

        var absDir = Path.Combine(projectDir, "abs_subdir");
        Directory.CreateDirectory(absDir);

        var absXml = Path.Combine(projectDir, "abs_test.xml");
        File.WriteAllText(absXml, "<AbsRoot />");

        var env = new TaskEnvironment { ProjectDirectory = projectDir };

        // DirectoryExists
        var unsafeDir = new Unsafe.RelativePathToDirectoryExists { InputPath = absDir, BuildEngine = Engine() };
        var fixedDir = new Fixed.RelativePathToDirectoryExists { InputPath = absDir, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeDir.Execute();
        fixedDir.Execute();
        Assert.Equal(unsafeDir.Result, fixedDir.Result);

        // FileExists
        var unsafeFile = new Unsafe.RelativePathToFileExists { InputPath = absFile, BuildEngine = Engine() };
        var fixedFile = new Fixed.RelativePathToFileExists { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeFile.Execute();
        fixedFile.Execute();
        Assert.Equal(unsafeFile.Result, fixedFile.Result);

        // FileStream
        var unsafeStream = new Unsafe.RelativePathToFileStream { InputPath = absFile, BuildEngine = Engine() };
        var fixedStream = new Fixed.RelativePathToFileStream { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeStream.Execute();
        fixedStream.Execute();
        Assert.Equal(unsafeStream.Result, fixedStream.Result);

        // XDocument
        var unsafeXml = new Unsafe.RelativePathToXDocument { InputPath = absXml, BuildEngine = Engine() };
        var fixedXml = new Fixed.RelativePathToXDocument { InputPath = absXml, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeXml.Execute();
        fixedXml.Execute();
        Assert.Equal(unsafeXml.Result, fixedXml.Result);

        // GetFullPath_AttributeOnly
        var unsafeAttr = new Unsafe.UsesPathGetFullPath_AttributeOnly { InputPath = absFile, BuildEngine = Engine() };
        var fixedAttr = new Fixed.UsesPathGetFullPath_AttributeOnly { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeAttr.Execute();
        fixedAttr.Execute();
        Assert.Equal(unsafeAttr.Result, fixedAttr.Result);

        // GetFullPath_ForCanonicalization
        var unsafeCanon = new Unsafe.UsesPathGetFullPath_ForCanonicalization { InputPath = absFile, BuildEngine = Engine() };
        var fixedCanon = new Fixed.UsesPathGetFullPath_ForCanonicalization { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeCanon.Execute();
        fixedCanon.Execute();
        Assert.Equal(unsafeCanon.Result, fixedCanon.Result);

        // GetFullPath_IgnoresTaskEnv
        var unsafeEnv = new Unsafe.UsesPathGetFullPath_IgnoresTaskEnv { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        var fixedEnv = new Fixed.UsesPathGetFullPath_IgnoresTaskEnv { InputPath = absFile, TaskEnvironment = env, BuildEngine = Engine() };
        unsafeEnv.Execute();
        fixedEnv.Execute();
        Assert.Equal(unsafeEnv.Result, fixedEnv.Result);
    }

    #endregion
}

internal class ContrastBuildEngine : IBuildEngine
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
