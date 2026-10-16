using System;
using System.Collections.Generic;
using System.IO;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core.Interfaces;

[TestSuite]
[RequireGodotRuntime]
public class ILogFallbackTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestWithoutASinkPrintAndWarningWriteToStandardOutAndErrorToStandardError()
    {
        var (output, error) = Capture(() =>
        {
            ILog.Print("one");
            ILog.Warning("two");
            ILog.Error("three");
        });

        Assertions.AssertThat(output).IsEqual("one" + Environment.NewLine + "two" + Environment.NewLine);
        Assertions.AssertThat(error).IsEqual("three" + Environment.NewLine);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestWithASinkEveryMessageReachesTheSinkAndTheConsoleStaysEmpty()
    {
        var sink = new RecordingLog();

        var (output, error) = Capture(() =>
        {
            ILog.Sink = sink;
            ILog.Print("one");
            ILog.Warning("two");
            ILog.Error("three");
        });

        Assertions.AssertThat(sink.Calls).IsEqual(new List<string> { "message:one", "warning:two", "error:three" });
        Assertions.AssertThat(output).IsEqual(string.Empty);
        Assertions.AssertThat(error).IsEqual(string.Empty);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestAfterAServicesResetTheFallbackWritesAndTheRootSinkReturns()
    {
        try
        {
            TestServices.Reset();
            var (output, _) = Capture(() => ILog.Print("after reset"));
            Assertions.AssertThat(ILog.Sink).IsNull();
            Assertions.AssertThat(output).IsEqual("after reset" + Environment.NewLine);

            TestServices.Reinitialize();
            var sink = new RecordingLog();
            var root = ILog.Sink;
            ILog.Sink = sink;
            ILog.Print("again");
            ILog.Sink = root;

            Assertions.AssertBool(root != null).IsTrue();
            Assertions.AssertThat(sink.Calls).IsEqual(new List<string> { "message:again" });
        }
        finally
        {
            TestServices.Reinitialize();
        }
    }

    // Runs the action with the console redirected and the sink cleared, then restores both.
    private static (string Output, string Error) Capture(Action action)
    {
        var previousOut = Console.Out;
        var previousError = Console.Error;
        var previousSink = ILog.Sink;
        var output = new StringWriter();
        var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            ILog.Sink = null;
            action();
            return (output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(previousOut);
            Console.SetError(previousError);
            ILog.Sink = previousSink;
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Calls { get; } = new();

        public void LogMessage(string message) => Calls.Add($"message:{message}");

        public void LogWarning(string message) => Calls.Add($"warning:{message}");

        public void LogError(string message) => Calls.Add($"error:{message}");

        public void LogError(string message, Exception exception) => Calls.Add($"error:{message}");
    }
}
