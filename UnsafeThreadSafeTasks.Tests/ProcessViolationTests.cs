using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

    #region Unsafe termination tasks — interface and property checks

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeProcess.CallsEnvironmentExit();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeProcess.CallsEnvironmentFailFast();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsProcessKill_DoesNotImplementIMultiThreadableTask()
    {
        var task = new UnsafeProcess.CallsProcessKill();
        Assert.IsNotAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_ExtendsTask()
    {
        var task = new UnsafeProcess.CallsEnvironmentExit();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_ExtendsTask()
    {
        var task = new UnsafeProcess.CallsEnvironmentFailFast();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsProcessKill_ExtendsTask()
    {
        var task = new UnsafeProcess.CallsProcessKill();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_HasExitCodeProperty()
    {
        var task = new UnsafeProcess.CallsEnvironmentExit { ExitCode = 42 };
        Assert.Equal(42, task.ExitCode);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_HasMessageProperty()
    {
        var task = new UnsafeProcess.CallsEnvironmentFailFast { Message = "test message" };
        Assert.Equal("test message", task.Message);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_MessageDefaultsToEmpty()
    {
        var task = new UnsafeProcess.CallsEnvironmentFailFast();
        Assert.Equal(string.Empty, task.Message);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_ExitCodeDefaultsToZero()
    {
        var task = new UnsafeProcess.CallsEnvironmentExit();
        Assert.Equal(0, task.ExitCode);
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Unsafe")]
    [MemberData(nameof(UnsafeTerminationTaskTypes))]
    public void UnsafeTerminationTask_DoesNotHaveTaskEnvironmentProperty(Type taskType)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.Null(prop);
    }

    public static IEnumerable<object[]> UnsafeTerminationTaskTypes()
    {
        yield return new object[] { typeof(UnsafeProcess.CallsEnvironmentExit) };
        yield return new object[] { typeof(UnsafeProcess.CallsEnvironmentFailFast) };
        yield return new object[] { typeof(UnsafeProcess.CallsProcessKill) };
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [MemberData(nameof(UnsafeTerminationTaskTypes))]
    public void UnsafeTerminationTask_DoesNotHaveMSBuildMultiThreadableTaskAttribute(Type taskType)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    #endregion

    #region Fixed termination tasks — interface and attribute checks

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_ImplementsIMultiThreadableTask(Type taskType, string _)
    {
        var task = Activator.CreateInstance(taskType)!;
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_HasMSBuildMultiThreadableTaskAttribute(Type taskType, string _)
    {
        var attr = Attribute.GetCustomAttribute(taskType, typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.NotNull(attr);
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_ErrorMessageMentionsForbiddenApi(Type taskType, string _)
    {
        var engine = new MockBuildEngine();
        var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
        task.BuildEngine = engine;

        task.Execute();

        Assert.Single(engine.Errors);
        Assert.Contains("must not call", engine.Errors[0].Message);
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_HasTaskEnvironmentProperty(Type taskType, string _)
    {
        var prop = taskType.GetProperty("TaskEnvironment");
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Theory]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    [MemberData(nameof(FixedProcessTerminationTasks))]
    public void FixedTerminationTask_LogsNoWarnings(Type taskType, string _)
    {
        var engine = new MockBuildEngine();
        var task = (MSBuildTask)Activator.CreateInstance(taskType)!;
        task.BuildEngine = engine;

        task.Execute();

        Assert.Empty(engine.Warnings);
    }

    #endregion

    #region Fixed termination tasks — specific error messages

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentExit_ErrorMessageMentionsEnvironmentExit()
    {
        var engine = new MockBuildEngine();
        var task = new FixedProcess.CallsEnvironmentExit { BuildEngine = engine };

        task.Execute();

        Assert.Single(engine.Errors);
        Assert.Contains("Environment.Exit", engine.Errors[0].Message);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentFailFast_ErrorMessageMentionsEnvironmentFailFast()
    {
        var engine = new MockBuildEngine();
        var task = new FixedProcess.CallsEnvironmentFailFast { BuildEngine = engine };

        task.Execute();

        Assert.Single(engine.Errors);
        Assert.Contains("Environment.FailFast", engine.Errors[0].Message);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsProcessKill_ErrorMessageMentionsProcessKill()
    {
        var engine = new MockBuildEngine();
        var task = new FixedProcess.CallsProcessKill { BuildEngine = engine };

        task.Execute();

        Assert.Single(engine.Errors);
        Assert.Contains("Process.GetCurrentProcess().Kill", engine.Errors[0].Message);
    }

    #endregion

    #region UsesRawProcessStartInfo — single execution and property tests

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_HasTaskEnvironmentProperty()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty("TaskEnvironment");
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_HasTaskEnvironmentProperty()
    {
        var prop = typeof(FixedProcess.UsesRawProcessStartInfo).GetProperty("TaskEnvironment");
        Assert.NotNull(prop);
        Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ImplementsIMultiThreadableTask()
    {
        var task = new UnsafeProcess.UsesRawProcessStartInfo();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_DoesNotHaveMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(UnsafeProcess.UsesRawProcessStartInfo),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void FixedUsesRawProcessStartInfo_HasMSBuildMultiThreadableTaskAttribute()
    {
        var attr = Attribute.GetCustomAttribute(
            typeof(FixedProcess.UsesRawProcessStartInfo),
            typeof(MSBuildMultiThreadableTaskAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_ImplementsIMultiThreadableTask()
    {
        var task = new FixedProcess.UsesRawProcessStartInfo();
        Assert.IsAssignableFrom<IMultiThreadableTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_ExtendsTask()
    {
        var task = new FixedProcess.UsesRawProcessStartInfo();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_SingleExecution_ReturnsOutput()
    {
        var task = new UnsafeProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = new TaskEnvironment(),
            Command = "cmd.exe",
            Arguments = "/c echo hello",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello", task.Result);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_SingleExecution_IgnoresProjectDirectory()
    {
        var dir = CreateTempDir();
        var task = new UnsafeProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir },
            Command = "cmd.exe",
            Arguments = "/c cd",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // Bug: working directory is process CWD, not ProjectDirectory
        Assert.NotEqual(dir, task.Result);
        Assert.Equal(Directory.GetCurrentDirectory(), task.Result);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void FixedUsesRawProcessStartInfo_SingleExecution_UsesProjectDirectory()
    {
        var dir = CreateTempDir();
        var task = new FixedProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = new TaskEnvironment { ProjectDirectory = dir },
            Command = "cmd.exe",
            Arguments = "/c cd",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal(dir, task.Result);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ExtendsTask()
    {
        var task = new UnsafeProcess.UsesRawProcessStartInfo();
        Assert.IsAssignableFrom<MSBuildTask>(task);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_CommandHasRequiredAttribute()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty(nameof(UnsafeProcess.UsesRawProcessStartInfo.Command));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ResultHasOutputAttribute()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty(nameof(UnsafeProcess.UsesRawProcessStartInfo.Result));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_DefaultProperties()
    {
        var task = new UnsafeProcess.UsesRawProcessStartInfo();

        Assert.Equal(string.Empty, task.Command);
        Assert.Equal(string.Empty, task.Arguments);
        Assert.Equal(string.Empty, task.Result);
        Assert.NotNull(task.TaskEnvironment);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_DoesNotPassEnvironmentVariables()
    {
        var uniqueVar = $"TEST_VAR_{Guid.NewGuid():N}";
        var env = new TaskEnvironment { ProjectDirectory = CreateTempDir() };
        env.SetEnvironmentVariable(uniqueVar, "expected_value");

        var task = new UnsafeProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = env,
            Command = "cmd.exe",
            Arguments = $"/c echo %{uniqueVar}%",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        // Bug: the environment variable is not passed to the spawned process
        Assert.DoesNotContain("expected_value", task.Result);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void FixedUsesRawProcessStartInfo_PassesEnvironmentVariables()
    {
        var uniqueVar = $"TEST_VAR_{Guid.NewGuid():N}";
        var env = new TaskEnvironment { ProjectDirectory = CreateTempDir() };
        env.SetEnvironmentVariable(uniqueVar, "expected_value");

        var task = new FixedProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = env,
            Command = "cmd.exe",
            Arguments = $"/c echo %{uniqueVar}%",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Contains("expected_value", task.Result);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_DefaultProperties()
    {
        var task = new FixedProcess.UsesRawProcessStartInfo();

        Assert.Equal(string.Empty, task.Command);
        Assert.Equal(string.Empty, task.Arguments);
        Assert.Equal(string.Empty, task.Result);
        Assert.NotNull(task.TaskEnvironment);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_CommandHasRequiredAttribute()
    {
        var prop = typeof(FixedProcess.UsesRawProcessStartInfo).GetProperty(nameof(FixedProcess.UsesRawProcessStartInfo.Command));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_ResultHasOutputAttribute()
    {
        var prop = typeof(FixedProcess.UsesRawProcessStartInfo).GetProperty(nameof(FixedProcess.UsesRawProcessStartInfo.Result));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(OutputAttribute));
        Assert.NotNull(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ArgumentsDoesNotHaveRequiredAttribute()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty(nameof(UnsafeProcess.UsesRawProcessStartInfo.Arguments));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_ArgumentsDoesNotHaveRequiredAttribute()
    {
        var prop = typeof(FixedProcess.UsesRawProcessStartInfo).GetProperty(nameof(FixedProcess.UsesRawProcessStartInfo.Arguments));
        Assert.NotNull(prop);
        var attr = Attribute.GetCustomAttribute(prop!, typeof(RequiredAttribute));
        Assert.Null(attr);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_SingleExecution_ReturnsOutput()
    {
        var task = new FixedProcess.UsesRawProcessStartInfo
        {
            TaskEnvironment = new TaskEnvironment(),
            Command = "cmd.exe",
            Arguments = "/c echo hello",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("hello", task.Result);
    }

    #endregion

    #region Unsafe task declared property surface area

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_HasOnlyExitCodeDeclaredProperty()
    {
        var props = typeof(UnsafeProcess.CallsEnvironmentExit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Single(props);
        Assert.Equal("ExitCode", props[0].Name);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_HasOnlyMessageDeclaredProperty()
    {
        var props = typeof(UnsafeProcess.CallsEnvironmentFailFast)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Single(props);
        Assert.Equal("Message", props[0].Name);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsProcessKill_HasNoDeclaredProperties()
    {
        var props = typeof(UnsafeProcess.CallsProcessKill)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Empty(props);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_HasExpectedDeclaredProperties()
    {
        var props = typeof(UnsafeProcess.UsesRawProcessStartInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var names = props.Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Arguments", "Command", "Result", "TaskEnvironment" }, names);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_ExitCodePropertyIsIntType()
    {
        var prop = typeof(UnsafeProcess.CallsEnvironmentExit).GetProperty("ExitCode");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_MessagePropertyIsStringType()
    {
        var prop = typeof(UnsafeProcess.CallsEnvironmentFailFast).GetProperty("Message");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentExit_ExitCodeIsReadWrite()
    {
        var prop = typeof(UnsafeProcess.CallsEnvironmentExit).GetProperty("ExitCode");
        Assert.NotNull(prop);
        Assert.True(prop!.CanRead);
        Assert.True(prop.CanWrite);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeCallsEnvironmentFailFast_MessageIsReadWrite()
    {
        var prop = typeof(UnsafeProcess.CallsEnvironmentFailFast).GetProperty("Message");
        Assert.NotNull(prop);
        Assert.True(prop!.CanRead);
        Assert.True(prop.CanWrite);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_CommandIsStringType()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty("Command");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ArgumentsIsStringType()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty("Arguments");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    public void UnsafeUsesRawProcessStartInfo_ResultIsStringType()
    {
        var prop = typeof(UnsafeProcess.UsesRawProcessStartInfo).GetProperty("Result");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    #endregion

    #region Fixed task declared property surface area and defaults

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentExit_HasExitCodeAndTaskEnvironmentDeclaredProperties()
    {
        var props = typeof(FixedProcess.CallsEnvironmentExit)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var names = props.Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "ExitCode", "TaskEnvironment" }, names);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentFailFast_HasMessageAndTaskEnvironmentDeclaredProperties()
    {
        var props = typeof(FixedProcess.CallsEnvironmentFailFast)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var names = props.Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Message", "TaskEnvironment" }, names);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsProcessKill_HasOnlyTaskEnvironmentDeclaredProperty()
    {
        var props = typeof(FixedProcess.CallsProcessKill)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Single(props);
        Assert.Equal("TaskEnvironment", props[0].Name);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedUsesRawProcessStartInfo_HasExpectedDeclaredProperties()
    {
        var props = typeof(FixedProcess.UsesRawProcessStartInfo)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var names = props.Select(p => p.Name).OrderBy(n => n).ToArray();
        Assert.Equal(new[] { "Arguments", "Command", "Result", "TaskEnvironment" }, names);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentExit_ExitCodePropertyIsIntType()
    {
        var prop = typeof(FixedProcess.CallsEnvironmentExit).GetProperty("ExitCode");
        Assert.NotNull(prop);
        Assert.Equal(typeof(int), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentFailFast_MessagePropertyIsStringType()
    {
        var prop = typeof(FixedProcess.CallsEnvironmentFailFast).GetProperty("Message");
        Assert.NotNull(prop);
        Assert.Equal(typeof(string), prop!.PropertyType);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentExit_DefaultExitCodeIsZero()
    {
        var task = new FixedProcess.CallsEnvironmentExit();
        Assert.Equal(0, task.ExitCode);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentFailFast_DefaultMessageIsEmpty()
    {
        var task = new FixedProcess.CallsEnvironmentFailFast();
        Assert.Equal(string.Empty, task.Message);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentExit_DefaultTaskEnvironmentIsNotNull()
    {
        var task = new FixedProcess.CallsEnvironmentExit();
        Assert.NotNull(task.TaskEnvironment);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsEnvironmentFailFast_DefaultTaskEnvironmentIsNotNull()
    {
        var task = new FixedProcess.CallsEnvironmentFailFast();
        Assert.NotNull(task.TaskEnvironment);
    }

    [Fact]
    [Trait("Category", "ProcessViolation")]
    [Trait("Target", "Fixed")]
    public void FixedCallsProcessKill_DefaultTaskEnvironmentIsNotNull()
    {
        var task = new FixedProcess.CallsProcessKill();
        Assert.NotNull(task.TaskEnvironment);
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
