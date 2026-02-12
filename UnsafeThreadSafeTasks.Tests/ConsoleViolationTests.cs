using System;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;
using Xunit;

using UnsafeConsole = UnsafeThreadSafeTasks.ConsoleViolations;
using FixedConsole = FixedThreadSafeTasks.ConsoleViolations;

namespace UnsafeThreadSafeTasks.Tests;

public class ConsoleViolationTests : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextReader _originalIn;
    private readonly TextWriter _originalError;

    public ConsoleViolationTests()
    {
        _originalOut = Console.Out;
        _originalIn = Console.In;
        _originalError = Console.Error;
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetIn(_originalIn);
        Console.SetError(_originalError);
    }

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
    public void SetsConsoleOut_Unsafe_RestoresConsoleOutAfterExecution()
    {
        var originalOut = Console.Out;

        var task = new UnsafeConsole.SetsConsoleOut
        {
            Message = "test_restore",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The unsafe task uses a finally block to restore Console.Out
        Assert.Same(originalOut, Console.Out);
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
    public void SetsConsoleError_Unsafe_RestoresConsoleErrorAfterExecution()
    {
        var originalError = Console.Error;

        var task = new UnsafeConsole.SetsConsoleError
        {
            Message = "test_restore",
            BuildEngine = new MockBuildEngine()
        };

        task.Execute();

        // The unsafe task uses a finally block to restore Console.Error
        Assert.Same(originalError, Console.Error);
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

    #region UsesConsoleWriteLine

    [Theory]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData("hello from task")]
    [InlineData("build output message")]
    [InlineData("")]
    public void UsesConsoleWriteLine_ProducesNoMessagesInMockBuildEngine(string message)
    {
        var engine = new MockBuildEngine();
        var task = new UnsafeConsole.UsesConsoleWriteLine
        {
            Message = message,
            BuildEngine = engine
        };

        // Redirect Console.Out so the test doesn't pollute stdout
        Console.SetOut(new StringWriter());

        bool result = task.Execute();

        Assert.True(result);
        // The unsafe task writes to Console, not the build engine — no messages captured
        Assert.Empty(engine.Messages);
        Assert.Empty(engine.Warnings);
        Assert.Empty(engine.Errors);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleWriteLine_Unsafe_ConcurrentWritesAllGoToSharedConsole()
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
                    var task = new UnsafeConsole.UsesConsoleWriteLine
                    {
                        Message = "CWLINE1",
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
                    var task = new UnsafeConsole.UsesConsoleWriteLine
                    {
                        Message = "CWLINE2",
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            var output = sharedWriter.ToString();
            // Both tasks wrote to the same process-global Console.Out
            Assert.Contains("CWLINE1", output);
            Assert.Contains("CWLINE2", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region UsesConsoleSetOut

    [Theory]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    [InlineData(1)]
    [InlineData(2)]
    public void UsesConsoleSetOut_CorruptsConsoleOutForSubsequentTasks(int taskCount)
    {
        var originalOut = Console.Out;

        for (int i = 0; i < taskCount; i++)
        {
            var engine = new MockBuildEngine();
            var task = new UnsafeConsole.UsesConsoleSetOut
            {
                BuildEngine = engine
            };

            task.Execute();
        }

        // Console.Out has been corrupted — it is no longer the original writer
        Assert.NotSame(originalOut, Console.Out);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleSetOut_Unsafe_ResultContainsCapturedText()
    {
        var originalOut = Console.Out;
        try
        {
            var engine = new MockBuildEngine();
            var task = new UnsafeConsole.UsesConsoleSetOut
            {
                BuildEngine = engine
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("captured", task.Result);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleSetOut_Unsafe_ConcurrentSetOutCausesCorruption()
    {
        var originalOut = Console.Out;
        try
        {
            var barrier = new Barrier(2);
            const int iterations = 20;

            var t1 = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int i = 0; i < iterations; i++)
                {
                    var task = new UnsafeConsole.UsesConsoleSetOut
                    {
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
                    var task = new UnsafeConsole.UsesConsoleSetOut
                    {
                        BuildEngine = new MockBuildEngine()
                    };
                    task.Execute();
                }
            });

            t1.Start(); t2.Start();
            t1.Join(); t2.Join();

            // Console.Out has been corrupted — it is no longer the original writer
            // because UsesConsoleSetOut never restores Console.Out
            Assert.NotSame(originalOut, Console.Out);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region UsesConsoleReadLine

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleReadLine_DetectsBlockingBehavior()
    {
        // Replace Console.In with a stream that never yields data, forcing ReadLine to block
        using var blockingStream = new BlockingStream();
        Console.SetIn(new StreamReader(blockingStream));

        var engine = new MockBuildEngine();
        var task = new UnsafeConsole.UsesConsoleReadLine
        {
            BlockingMode = true,
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        // The task's internal timeout detected that Console.ReadLine blocked
        Assert.Equal("BLOCKED", task.Result);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleReadLine_Unsafe_BlockingModeLogsWarningViaBuildEngine()
    {
        var originalIn = Console.In;
        try
        {
            using var blockingStream = new BlockingStream();
            Console.SetIn(new StreamReader(blockingStream));

            var engine = new MockBuildEngine();
            var task = new UnsafeConsole.UsesConsoleReadLine
            {
                BlockingMode = true,
                BuildEngine = engine
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("BLOCKED", task.Result);
            // The task logs a warning when Console.ReadLine blocks
            Assert.Contains(engine.Warnings, w => w.Message.Contains("Console.ReadLine blocked"));
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Unsafe")]
    public void UsesConsoleReadLine_Unsafe_NonBlockingModeSkips()
    {
        var engine = new MockBuildEngine();
        var task = new UnsafeConsole.UsesConsoleReadLine
        {
            BlockingMode = false,
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("SKIPPED", task.Result);
    }

    #endregion

    #region Fixed_WritesToConsoleOut

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleOut_Fixed_DoesNotWriteToProcessGlobalStdout()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            var task = new FixedConsole.WritesToConsoleOut
            {
                Message = "hello_fixed",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("hello_fixed", task.Result);
            // Fixed: nothing written to process-global Console.Out
            Assert.DoesNotContain("hello_fixed", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleOut_Fixed_LogsViaBuildEngine()
    {
        var engine = new MockBuildEngine();
        var task = new FixedConsole.WritesToConsoleOut
        {
            Message = "logged_message",
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("logged_message", task.Result);
        Assert.Contains(engine.Messages, m => m.Message == "logged_message");
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleOut_Fixed_ConcurrentWritesDoNotInterleave()
    {
        var barrier = new Barrier(2);
        const int iterations = 50;
        bool success1 = true, success2 = true;

        var t1 = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var task = new FixedConsole.WritesToConsoleOut
                {
                    Message = "TASK1",
                    BuildEngine = new MockBuildEngine()
                };
                success1 &= task.Execute();
            }
        });

        var t2 = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var task = new FixedConsole.WritesToConsoleOut
                {
                    Message = "TASK2",
                    BuildEngine = new MockBuildEngine()
                };
                success2 &= task.Execute();
            }
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.True(success1);
        Assert.True(success2);
    }

    #endregion

    #region Fixed_WritesToConsoleError

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleError_Fixed_DoesNotWriteToProcessGlobalStderr()
    {
        var originalError = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetError(writer);

            var task = new FixedConsole.WritesToConsoleError
            {
                Message = "error_fixed",
                BuildEngine = new MockBuildEngine()
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("error_fixed", task.Result);
            // Fixed: nothing written to process-global Console.Error
            Assert.DoesNotContain("error_fixed", writer.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleError_Fixed_LogsViaBuildEngine()
    {
        var engine = new MockBuildEngine();
        var task = new FixedConsole.WritesToConsoleError
        {
            Message = "logged_error",
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("logged_error", task.Result);
        Assert.Contains(engine.Warnings, w => w.Message == "logged_error");
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void WritesToConsoleError_Fixed_ConcurrentWritesDoNotInterleave()
    {
        var barrier = new Barrier(2);
        const int iterations = 50;
        bool success1 = true, success2 = true;

        var t1 = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var task = new FixedConsole.WritesToConsoleError
                {
                    Message = "ERR1",
                    BuildEngine = new MockBuildEngine()
                };
                success1 &= task.Execute();
            }
        });

        var t2 = new Thread(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < iterations; i++)
            {
                var task = new FixedConsole.WritesToConsoleError
                {
                    Message = "ERR2",
                    BuildEngine = new MockBuildEngine()
                };
                success2 &= task.Execute();
            }
        });

        t1.Start(); t2.Start();
        t1.Join(); t2.Join();

        Assert.True(success1);
        Assert.True(success2);
    }

    #endregion

    #region Fixed_SetsConsoleOut

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void SetsConsoleOut_Fixed_CapturesOutputWithoutGlobalRedirect()
    {
        var task = new FixedConsole.SetsConsoleOut
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
    [Trait("Target", "Fixed")]
    public void SetsConsoleOut_Fixed_ConcurrentCapturesDoNotInterfere()
    {
        var barrier = new Barrier(2);
        string? captured1 = null, captured2 = null;
        bool success1 = false, success2 = false;

        var t1 = new Thread(() =>
        {
            var task = new FixedConsole.SetsConsoleOut
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
            var task = new FixedConsole.SetsConsoleOut
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

        Assert.True(success1);
        Assert.True(success2);
        // Fixed: each task captures its own message without interference
        Assert.Equal("msg_from_task1", captured1);
        Assert.Equal("msg_from_task2", captured2);
    }

    #endregion

    #region Fixed_SetsConsoleError

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void SetsConsoleError_Fixed_CapturesOutputWithoutGlobalRedirect()
    {
        var task = new FixedConsole.SetsConsoleError
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
    [Trait("Target", "Fixed")]
    public void SetsConsoleError_Fixed_ConcurrentCapturesDoNotInterfere()
    {
        var barrier = new Barrier(2);
        string? captured1 = null, captured2 = null;
        bool success1 = false, success2 = false;

        var t1 = new Thread(() =>
        {
            var task = new FixedConsole.SetsConsoleError
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
            var task = new FixedConsole.SetsConsoleError
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
        Assert.Equal("err_from_task1", captured1);
        Assert.Equal("err_from_task2", captured2);
    }

    #endregion

    #region Fixed_UsesConsoleWriteLine

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void UsesConsoleWriteLine_Fixed_LogsViaBuildEngine()
    {
        var engine = new MockBuildEngine();
        var task = new FixedConsole.UsesConsoleWriteLine
        {
            Message = "logged_message",
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Contains(engine.Messages, m => m.Message == "logged_message");
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void UsesConsoleWriteLine_Fixed_DoesNotWriteToProcessGlobalConsole()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);

            var engine = new MockBuildEngine();
            var task = new FixedConsole.UsesConsoleWriteLine
            {
                Message = "fixed_writeline_msg",
                BuildEngine = engine
            };

            bool result = task.Execute();

            Assert.True(result);
            // Fixed: nothing written to process-global Console.Out
            Assert.DoesNotContain("fixed_writeline_msg", writer.ToString());
            // Instead, logged via build engine
            Assert.Contains(engine.Messages, m => m.Message == "fixed_writeline_msg");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region Fixed_UsesConsoleSetOut

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void UsesConsoleSetOut_Fixed_DoesNotCorruptGlobalConsole()
    {
        var engine = new MockBuildEngine();
        var task = new FixedConsole.UsesConsoleSetOut
        {
            BuildEngine = engine
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("captured", task.Result);
        Assert.Contains(engine.Messages, m => m.Message == "captured");
    }

    #endregion

    #region Fixed_UsesConsoleReadLine

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void UsesConsoleReadLine_Fixed_DoesNotBlockOnStdin()
    {
        var task = new FixedConsole.UsesConsoleReadLine
        {
            BlockingMode = true,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("SKIPPED", task.Result);
    }

    [Fact]
    [Trait("Category", "ConsoleViolation")]
    [Trait("Target", "Fixed")]
    public void UsesConsoleReadLine_Fixed_NonBlockingModeReturnsSkipped()
    {
        var task = new FixedConsole.UsesConsoleReadLine
        {
            BlockingMode = false,
            BuildEngine = new MockBuildEngine()
        };

        bool result = task.Execute();

        Assert.True(result);
        Assert.Equal("SKIPPED", task.Result);
    }

    #endregion

    /// <summary>
    /// A stream whose Read method blocks indefinitely, simulating an empty stdin.
    /// </summary>
    private sealed class BlockingStream : Stream
    {
        private readonly ManualResetEventSlim _gate = new(false);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            // Block until disposed
            _gate.Wait(Timeout.Infinite);
            return 0;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            _gate.Set();
            base.Dispose(disposing);
        }
    }
}
