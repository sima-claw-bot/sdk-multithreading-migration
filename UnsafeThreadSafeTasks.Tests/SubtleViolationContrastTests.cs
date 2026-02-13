#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

using UnsafeSubtle = UnsafeThreadSafeTasks.SubtleViolations;
using FixedSubtle = FixedThreadSafeTasks.SubtleViolations;

namespace UnsafeThreadSafeTasks.Tests;

/// <summary>
/// Contrast tests that directly compare unsafe SubtleViolation tasks against their
/// fixed counterparts. Each test verifies the specific thread-safety bug in the unsafe
/// version and confirms the fixed version resolves it correctly.
/// </summary>
[Trait("Category", "SubtleViolation")]
[Trait("Target", "Contrast")]
public class SubtleViolationContrastTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"svcontrast_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return Path.GetFullPath(dir);
    }

    private static SubtleContrastBuildEngine Engine() => new();

    #region TaskTheta05: static vs instance field

    [Fact]
    public void SharedMutableStaticField_UnsafeUsesStaticField_FixedUsesInstanceField()
    {
        var unsafeField = typeof(UnsafeSubtle.TaskTheta05)
            .GetField("_lastValue", BindingFlags.Static | BindingFlags.NonPublic);
        var fixedField = typeof(FixedSubtle.TaskTheta05)
            .GetField("_lastValue", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(unsafeField);
        Assert.True(unsafeField!.IsStatic, "Unsafe _lastValue should be static");

        Assert.NotNull(fixedField);
        Assert.False(fixedField!.IsStatic, "Fixed _lastValue should be instance");
    }

    [Fact]
    public void SharedMutableStaticField_UnsafeCrossContaminates_FixedIsolates()
    {
        // --- Fixed version: each thread gets its own result ---
        var barrier1 = new Barrier(2);
        string? fixedResult1 = null, fixedResult2 = null;

        var ft1 = new Thread(() =>
        {
            var task = new FixedSubtle.TaskTheta05
            {
                InputValue = "alpha",
                BuildEngine = Engine(),
                TaskEnvironment = new TaskEnvironment()
            };
            barrier1.SignalAndWait();
            task.Execute();
            fixedResult1 = task.Result;
        });

        var ft2 = new Thread(() =>
        {
            var task = new FixedSubtle.TaskTheta05
            {
                InputValue = "beta",
                BuildEngine = Engine(),
                TaskEnvironment = new TaskEnvironment()
            };
            barrier1.SignalAndWait();
            task.Execute();
            fixedResult2 = task.Result;
        });

        ft1.Start(); ft2.Start();
        ft1.Join(); ft2.Join();

        // Fixed: each task reads its own value
        Assert.Equal("alpha", fixedResult1);
        Assert.Equal("beta", fixedResult2);

        // --- Unsafe version: static field causes cross-contamination ---
        var barrier2 = new Barrier(2);
        string? unsafeResult1 = null, unsafeResult2 = null;

        var ut1 = new Thread(() =>
        {
            var task = new UnsafeSubtle.TaskTheta05
            {
                InputValue = "alpha",
                BuildEngine = Engine()
            };
            barrier2.SignalAndWait();
            task.Execute();
            unsafeResult1 = task.Result;
        });

        var ut2 = new Thread(() =>
        {
            var task = new UnsafeSubtle.TaskTheta05
            {
                InputValue = "beta",
                BuildEngine = Engine()
            };
            barrier2.SignalAndWait();
            task.Execute();
            unsafeResult2 = task.Result;
        });

        ut1.Start(); ut2.Start();
        ut1.Join(); ut2.Join();

        // Unsafe: static field cross-contamination — both threads see the same value
        Assert.Equal(unsafeResult1, unsafeResult2);
    }

    [Fact]
    public void SharedMutableStaticField_UnsafeNotMultiThreadable_FixedIs()
    {
        Assert.False(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(UnsafeSubtle.TaskTheta05)),
            "Unsafe TaskTheta05 should NOT implement IMultiThreadableTask");
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(FixedSubtle.TaskTheta05)),
            "Fixed TaskTheta05 should implement IMultiThreadableTask");
    }

    [Fact]
    public void SharedMutableStaticField_UnsafeNoAttribute_FixedHasAttribute()
    {
        var unsafeAttr = typeof(UnsafeSubtle.TaskTheta05)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        var fixedAttr = typeof(FixedSubtle.TaskTheta05)
            .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();

        Assert.Null(unsafeAttr);
        Assert.NotNull(fixedAttr);
    }

    #endregion

    #region TaskTheta01: CWD vs TaskEnvironment

    [Fact]
    public void DoubleResolvesPath_UnsafeResolvesCwd_FixedResolvesProjectDir()
    {
        var projDir = CreateTempDir();
        var relativePath = Path.Combine("sub", "file.txt");

        // Unsafe: resolves against process CWD
        var unsafeTask = new UnsafeSubtle.TaskTheta01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        Assert.DoesNotContain(projDir, unsafeTask.Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.GetFullPath(relativePath), unsafeTask.Result);

        // Fixed: resolves against ProjectDirectory
        var fixedTask = new FixedSubtle.TaskTheta01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        Assert.StartsWith(projDir, fixedTask.Result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.Combine(projDir, relativePath), fixedTask.Result);
    }

    [Fact]
    public void DoubleResolvesPath_BothReturnAbsoluteButFromDifferentBases()
    {
        var projDir = CreateTempDir();
        var relativePath = "myfile.txt";

        var unsafeTask = new UnsafeSubtle.TaskTheta01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        // Both return absolute paths
        Assert.True(Path.IsPathRooted(unsafeTask.Result));
        Assert.True(Path.IsPathRooted(fixedTask.Result));

        // But from different bases (CWD vs ProjectDirectory)
        Assert.NotEqual(unsafeTask.Result, fixedTask.Result);
    }

    [Fact]
    public void DoubleResolvesPath_AbsoluteInput_BothReturnSameResult()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");

        var unsafeTask = new UnsafeSubtle.TaskTheta01
        {
            InputPath = absPath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta01
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
            InputPath = absPath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        // Absolute paths pass through unchanged in both
        Assert.Equal(absPath, unsafeTask.Result);
        Assert.Equal(absPath, fixedTask.Result);
    }

    #endregion

    #region TaskTheta02: hidden CWD resolution vs TaskEnvironment

    [Fact]
    public void IndirectPathGetFullPath_UnsafeResolvesCwd_FixedResolvesProjectDir()
    {
        var projDir = CreateTempDir();
        var relativePath = Path.Combine("sub", "file.txt");

        // Unsafe: private ResolvePath uses Path.GetFullPath → CWD
        var unsafeTask = new UnsafeSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        Assert.DoesNotContain(projDir, unsafeTask.Result, StringComparison.OrdinalIgnoreCase);

        // Fixed: private ResolvePath uses TaskEnvironment.GetAbsolutePath
        var fixedTask = new FixedSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        Assert.StartsWith(projDir, fixedTask.Result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IndirectPathGetFullPath_ContrastResults_DifferForRelativePaths()
    {
        var projDir = CreateTempDir();
        var relativePath = "data.bin";

        var unsafeTask = new UnsafeSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputPath = relativePath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        // Different resolution bases produce different results
        Assert.NotEqual(unsafeTask.Result, fixedTask.Result);
        Assert.Equal(Path.GetFullPath(relativePath), unsafeTask.Result);
        Assert.Equal(Path.Combine(projDir, relativePath), fixedTask.Result);
    }

    [Fact]
    public void IndirectPathGetFullPath_AbsoluteInput_BothReturnSameResult()
    {
        var absPath = Path.Combine(CreateTempDir(), "file.txt");

        var unsafeTask = new UnsafeSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
            InputPath = absPath,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta02
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = CreateTempDir() },
            InputPath = absPath,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        Assert.Equal(absPath, unsafeTask.Result);
        Assert.Equal(absPath, fixedTask.Result);
    }

    #endregion

    #region TaskTheta03: Environment.CurrentDirectory vs ProjectDirectory

    [Fact]
    public void LambdaCapturesCurrentDirectory_UnsafeUsesCwd_FixedUsesProjectDir()
    {
        var projDir = CreateTempDir();
        var files = new ITaskItem[]
        {
            new TaskItem("file1.txt"),
            new TaskItem("file2.txt")
        };

        // Unsafe: lambda captures Environment.CurrentDirectory
        var unsafeTask = new UnsafeSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = files,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        foreach (var path in unsafeTask.ResolvedPaths)
        {
            Assert.StartsWith(Environment.CurrentDirectory, path, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(projDir, path, StringComparison.OrdinalIgnoreCase);
        }

        // Fixed: uses TaskEnvironment.ProjectDirectory
        var fixedTask = new FixedSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = files,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        foreach (var path in fixedTask.ResolvedPaths)
        {
            Assert.StartsWith(projDir, path, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void LambdaCapturesCurrentDirectory_ContrastResults_DifferForSameInput()
    {
        var projDir = CreateTempDir();
        var files = new ITaskItem[] { new TaskItem("test.txt") };

        var unsafeTask = new UnsafeSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = files,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = files,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        Assert.Single(unsafeTask.ResolvedPaths);
        Assert.Single(fixedTask.ResolvedPaths);
        Assert.NotEqual(unsafeTask.ResolvedPaths[0], fixedTask.ResolvedPaths[0]);
    }

    [Fact]
    public void LambdaCapturesCurrentDirectory_EmptyInput_BothReturnEmpty()
    {
        var projDir = CreateTempDir();
        var emptyFiles = Array.Empty<ITaskItem>();

        var unsafeTask = new UnsafeSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = emptyFiles,
            BuildEngine = Engine()
        };
        unsafeTask.Execute();

        var fixedTask = new FixedSubtle.TaskTheta03
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
            InputFiles = emptyFiles,
            BuildEngine = Engine()
        };
        fixedTask.Execute();

        Assert.Empty(unsafeTask.ResolvedPaths);
        Assert.Empty(fixedTask.ResolvedPaths);
    }

    #endregion

    #region TaskTheta04: path correct but env wrong vs both correct

    [Fact]
    public void PartialMigration_UnsafeEnvReadsGlobal_FixedEnvReadsTaskEnvironment()
    {
        var projDir = CreateTempDir();
        var varName = $"SV_CONTRAST_{Guid.NewGuid():N}";
        var originalVal = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "global_value");

            var env1 = new TaskEnvironment { ProjectDirectory = projDir };
            env1.SetEnvironmentVariable(varName, "task_env_value");

            // Unsafe: reads from process-global Environment
            var unsafeTask = new UnsafeSubtle.TaskTheta04
            {
                TaskEnvironment = env1,
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            Assert.Equal("global_value", unsafeTask.EnvResult);

            var env2 = new TaskEnvironment { ProjectDirectory = projDir };
            env2.SetEnvironmentVariable(varName, "task_env_value");

            // Fixed: reads from TaskEnvironment
            var fixedTask = new FixedSubtle.TaskTheta04
            {
                TaskEnvironment = env2,
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            Assert.Equal("task_env_value", fixedTask.EnvResult);

            // Contrast: same variable name, different results
            Assert.NotEqual(unsafeTask.EnvResult, fixedTask.EnvResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalVal);
        }
    }

    [Fact]
    public void PartialMigration_BothResolvePathViaTaskEnvironment()
    {
        var projDir = CreateTempDir();
        var varName = $"SV_PATH_{Guid.NewGuid():N}";
        var originalVal = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, "val");

            var unsafeTask = new UnsafeSubtle.TaskTheta04
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            var fixedTask = new FixedSubtle.TaskTheta04
            {
                TaskEnvironment = new TaskEnvironment { ProjectDirectory = projDir },
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            // Both use TaskEnvironment for path resolution
            Assert.StartsWith(projDir, unsafeTask.PathResult, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(projDir, fixedTask.PathResult, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(unsafeTask.PathResult, fixedTask.PathResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalVal);
        }
    }

    [Fact]
    public void PartialMigration_UnsafeMissingEnvVar_ReturnsEmpty_FixedReadsFromTaskEnv()
    {
        var projDir = CreateTempDir();
        var varName = $"SV_MISSING_{Guid.NewGuid():N}";
        // Ensure variable is NOT set globally
        var originalVal = Environment.GetEnvironmentVariable(varName);

        try
        {
            Environment.SetEnvironmentVariable(varName, null);

            var unsafeEnv = new TaskEnvironment { ProjectDirectory = projDir };
            unsafeEnv.SetEnvironmentVariable(varName, "only_in_task_env");

            var unsafeTask = new UnsafeSubtle.TaskTheta04
            {
                TaskEnvironment = unsafeEnv,
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            unsafeTask.Execute();

            // Unsafe reads from process-global → not found → empty
            Assert.Equal(string.Empty, unsafeTask.EnvResult);

            var fixedEnv = new TaskEnvironment { ProjectDirectory = projDir };
            fixedEnv.SetEnvironmentVariable(varName, "only_in_task_env");

            var fixedTask = new FixedSubtle.TaskTheta04
            {
                TaskEnvironment = fixedEnv,
                VariableName = varName,
                InputPath = "sub",
                BuildEngine = Engine()
            };
            fixedTask.Execute();

            // Fixed reads from TaskEnvironment → found
            Assert.Equal("only_in_task_env", fixedTask.EnvResult);
        }
        finally
        {
            Environment.SetEnvironmentVariable(varName, originalVal);
        }
    }

    #endregion

    #region Cross-cutting: all tasks inherit from MSBuild Task

    [Theory]
    [InlineData(typeof(UnsafeSubtle.TaskTheta05))]
    [InlineData(typeof(UnsafeSubtle.TaskTheta01))]
    [InlineData(typeof(UnsafeSubtle.TaskTheta02))]
    [InlineData(typeof(UnsafeSubtle.TaskTheta03))]
    [InlineData(typeof(UnsafeSubtle.TaskTheta04))]
    [InlineData(typeof(FixedSubtle.TaskTheta05))]
    [InlineData(typeof(FixedSubtle.TaskTheta01))]
    [InlineData(typeof(FixedSubtle.TaskTheta02))]
    [InlineData(typeof(FixedSubtle.TaskTheta03))]
    [InlineData(typeof(FixedSubtle.TaskTheta04))]
    public void AllSubtleViolationTasks_InheritFromMSBuildTask(Type taskType)
    {
        Assert.True(typeof(Microsoft.Build.Utilities.Task).IsAssignableFrom(taskType));
    }

    [Theory]
    [InlineData(typeof(FixedSubtle.TaskTheta05))]
    [InlineData(typeof(FixedSubtle.TaskTheta01))]
    [InlineData(typeof(FixedSubtle.TaskTheta02))]
    [InlineData(typeof(FixedSubtle.TaskTheta03))]
    [InlineData(typeof(FixedSubtle.TaskTheta04))]
    public void AllFixedTasks_HaveMultiThreadableAttribute(Type taskType)
    {
        var attr = taskType.GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
        Assert.NotNull(attr);
    }

    [Theory]
    [InlineData(typeof(FixedSubtle.TaskTheta05))]
    [InlineData(typeof(FixedSubtle.TaskTheta01))]
    [InlineData(typeof(FixedSubtle.TaskTheta02))]
    [InlineData(typeof(FixedSubtle.TaskTheta03))]
    [InlineData(typeof(FixedSubtle.TaskTheta04))]
    public void AllFixedTasks_ImplementIMultiThreadableTask(Type taskType)
    {
        Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(taskType));
    }

    #endregion
}

internal class SubtleContrastBuildEngine : IBuildEngine
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
