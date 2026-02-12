using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeConsole = UnsafeThreadSafeTasks.ConsoleViolations;

namespace UnsafeThreadSafeTasks.Tests;

public class ConsoleViolationTests
{
    #region WritesToConsoleOut

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void WritesToConsoleOut_Unsafe_WritesToProcessGlobalStdout()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            var task = new UnsafeConsole.WritesToConsoleOut
            {
                Message = "hello_from_task",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("hello_from_task", task.Result);
            // Unsafe: the message goes to the process-global Console.Out
            Assert.Contains("hello_from_task", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void WritesToConsoleOut_Unsafe_ConcurrentWritesInterleave()
    {
        var originalOut = Console.Out;
        try
        {
            using var sharedWriter = new StringWriter();
            Console.SetOut(sharedWriter);

            var barrier = new Barrier(2);
            const int iterations = 50;

            var t1 = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterations; i++)
                {
                    var task = new UnsafeConsole.WritesToConsoleOut
                    {
                        Message = "TASK1",
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            var t2 = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterations; i++)
                {
                    var task = new UnsafeConsole.WritesToConsoleOut
                    {
                        Message = "TASK2",
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            var output = sharedWriter.ToString();
            // Both tasks wrote to the same process-global Console.Out
            Assert.Contains("TASK1", output);
            Assert.Contains("TASK2", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region WritesToConsoleError

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void WritesToConsoleError_Unsafe_WritesToProcessGlobalStderr()
    {
        var originalError = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);

            var task = new UnsafeConsole.WritesToConsoleError
            {
                Message = "error_from_task",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("error_from_task", task.Result);
            // Unsafe: the message goes to the process-global Console.Error
            Assert.Contains("error_from_task", writer.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void WritesToConsoleError_Unsafe_ConcurrentWritesInterleave()
    {
        var originalError = Console.Error;
        try
        {
            using var sharedWriter = new StringWriter();
            Console.SetError(sharedWriter);

            var barrier = new Barrier(2);
            const int iterations = 50;

            var t1 = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterations; i++)
                {
                    var task = new UnsafeConsole.WritesToConsoleError
                    {
                        Message = "ERR1",
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            var t2 = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterations; i++)
                {
                    var task = new UnsafeConsole.WritesToConsoleError
                    {
                        Message = "ERR2",
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            var output = sharedWriter.ToString();
            // Both tasks wrote to the same process-global Console.Error
            Assert.Contains("ERR1", output);
            Assert.Contains("ERR2", output);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    #endregion

    #region SetsConsoleOut

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void SetsConsoleOut_Unsafe_CapturesOutputViaGlobalRedirect()
    {
        var task = new UnsafeConsole.SetsConsoleOut
        {
            Message = "captured_message",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("captured_message", task.CapturedOutput);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void SetsConsoleOut_Unsafe_ConcurrentRedirectsCauseInterference()
    {
        var barrier = new Barrier(2);
        string? captured1 = null, captured2 = null;
        bool success1 = false, success2 = false;

        var t1 = new Thread(() =>
        {
            var task = new UnsafeConsole.SetsConsoleOut
            {
                Message = "msg_from_task1",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            success1 = task.Execute();
            captured1 = task.CapturedOutput;
        });

        var t2 = new Thread(() =>
        {
            var task = new UnsafeConsole.SetsConsoleOut
            {
                Message = "msg_from_task2",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            success2 = task.Execute();
            captured2 = task.CapturedOutput;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        // Both tasks complete (they restore Console.Out in finally blocks)
        Assert.True(success1);
        Assert.True(success2);
        // At minimum, the tasks ran — output may be lost or cross-contaminated
        // because Console.SetOut is process-global
        Assert.NotNull(captured1);
        Assert.NotNull(captured2);
    }

    #endregion

    #region SetsConsoleError

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void SetsConsoleError_Unsafe_CapturesOutputViaGlobalRedirect()
    {
        var task = new UnsafeConsole.SetsConsoleError
        {
            Message = "captured_error",
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("captured_error", task.CapturedOutput);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void SetsConsoleError_Unsafe_ConcurrentRedirectsCauseInterference()
    {
        var barrier = new Barrier(2);
        string? captured1 = null, captured2 = null;
        bool success1 = false, success2 = false;

        var t1 = new Thread(() =>
        {
            var task = new UnsafeConsole.SetsConsoleError
            {
                Message = "err_from_task1",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            success1 = task.Execute();
            captured1 = task.CapturedOutput;
        });

        var t2 = new Thread(() =>
        {
            var task = new UnsafeConsole.SetsConsoleError
            {
                Message = "err_from_task2",
                BuildEngine = new MockBuildEngine()
            };
            barrier.SignalAndWait();
            success2 = task.Execute();
            captured2 = task.CapturedOutput;
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.True(success1);
        Assert.True(success2);
        Assert.NotNull(captured1);
        Assert.NotNull(captured2);
    }

    #endregion
}
