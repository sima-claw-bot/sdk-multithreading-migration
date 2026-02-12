using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xunit;

using FixedProcess = FixedThreadSafeTasks.ProcessViolations;
using UnsafeProcess = UnsafeThreadSafeTasks.ProcessViolations;
using MSBuildTask = Microsoft.Build.Utilities.Task;

namespace UnsafeThreadSafeTasks.Tests;

public class ProcessViolationTests : IDisposable
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
        var dir = Path.Combine(Path.GetTempPath(), $"ProcViolation_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    #region UsesRawProcessStartInfo — concurrent working-directory tests

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task FixedUsesRawProcessStartInfo_ConcurrentExecution_EachProcessUsesIntendedWorkingDirectory(int concurrency)
    {
        var dirs = Enumerable.Range(0, concurrency).Select(_ => CreateTempDir()).ToArray();

        var tasks = new Task<(string expected, string actual)>[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            var dir = dirs[i];
            tasks[i] = Task.Run(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir };
                var task = new FixedProcess.UsesRawProcessStartInfo
                {
                    TaskEnvironment = env,
                    Command = "cmd.exe",
                    Arguments = "/c cd",
                    BuildEngine = new MockBuildEngine()
                };

                bool result = task.Execute();
                Assert.True(result, "Fixed UsesRawProcessStartInfo should succeed");
                return (expected: dir, actual: task.Result);
            });
        }

        var results = await Task.WhenAll(tasks);

        foreach (var (expected, actual) in results)
        {
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(2)]
    [InlineData(4)]
    public async Task UnsafeUsesRawProcessStartInfo_ConcurrentExecution_AllProcessesUseProcessCwd(int concurrency)
    {
        var dirs = Enumerable.Range(0, concurrency).Select(_ => CreateTempDir()).ToArray();
        var processCwd = Directory.GetCurrentDirectory();

        var tasks = new Task<string>[concurrency];
        for (int i = 0; i < concurrency; i++)
        {
            var dir = dirs[i];
            tasks[i] = Task.Run(() =>
            {
                var env = new TaskEnvironment { ProjectDirectory = dir };
                var task = new UnsafeProcess.UsesRawProcessStartInfo
                {
                    TaskEnvironment = env,
                    Command = "cmd.exe",
                    Arguments = "/c cd",
                    BuildEngine = new MockBuildEngine()
                };

                bool result = task.Execute();
                Assert.True(result, "Unsafe UsesRawProcessStartInfo should succeed");
                return task.Result;
            });
        }

        var results = await Task.WhenAll(tasks);

        // Bug: all processes use the test runner's CWD, not their intended ProjectDirectory
        foreach (var actual in results)
        {
            Assert.Equal(processCwd, actual);
        }
    }

    #endregion

    #region Exit / FailFast / Kill — fixed versions return false with errors

    public static IEnumerable<object[]> FixedProcessTerminationTasks()
    {
        yield return new object[] { typeof(FixedProcess.CallsEnvironmentExit), nameof(FixedProcess.CallsEnvironmentExit) };
        yield return new object[] { typeof(FixedProcess.CallsEnvironmentFailFast), nameof(FixedProcess.CallsEnvironmentFailFast) };
        yield return new object[] { typeof(FixedProcess.CallsProcessKill), nameof(FixedProcess.CallsProcessKill) };
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_Execute_ReturnsFalseWithErrors(Type taskType, string _)
    {
        var engine = new MockBuildEngine();
        var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
        task.BuildEngine = engine;

        bool result = task.Execute();

        Assert.False(result);
        Assert.NotEmpty(engine.Errors);
    }

    #endregion
}

/// <summary>
/// Build engine mock that captures errors, warnings, and messages for test assertions.
/// </summary>
internal class MockBuildEngine : IBuildEngine
{
    public List<BuildErrorEventArgs> Errors { get; } = new();
    public List<BuildWarningEventArgs> Warnings { get; } = new();
    public List<BuildMessageEventArgs> Messages { get; } = new();

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => string.Empty;

    public bool BuildProjectFile(string projectFileName, string[] targetNames,
        System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs) => true;

    public void LogCustomEvent(CustomBuildEventArgs e) { }
    public void LogErrorEvent(BuildErrorEventArgs e) => Errors.Add(e);
    public void LogMessageEvent(BuildMessageEventArgs e) => Messages.Add(e);
    public void LogWarningEvent(BuildWarningEventArgs e) => Warnings.Add(e);
}
