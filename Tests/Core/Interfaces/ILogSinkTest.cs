using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Core.Interfaces;

[TestSuite]
[RequireGodotRuntime]
public class ILogSinkTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestStaticHelpersWriteToTheSink()
    {
        var previous = ILog.Sink;
        var sink = new RecordingLog();
        try
        {
            ILog.Sink = sink;

            ILog.Print("first");
            ILog.Warning("second");
            ILog.Error("third");

            Assertions.AssertThat(sink.Calls)
                .IsEqual(new List<string> { "message:first", "warning:second", "error:third" });
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestSinkIsTheRegisteredLoggerWhileTheLocatorRuns()
    {
        ServiceLocator.ReinitializeServices();

        Assertions.AssertBool(ReferenceEquals(ILog.Sink, ServiceLocator.Get<ILog>())).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestResetClearsTheSinkAndPrintStillWorks()
    {
        try
        {
            ServiceLocator.ResetForTesting();

            Assertions.AssertThat(ILog.Sink).IsNull();
            ILog.Print("ILog sink reset probe");
        }
        finally
        {
            ServiceLocator.ReinitializeServices();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestReinitializeSetsTheSinkAgain()
    {
        ServiceLocator.ResetForTesting();
        ServiceLocator.ReinitializeServices();

        Assertions.AssertThat(ILog.Sink).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestSinkPrefixNamesTheCaller()
    {
        // Expected console line: [ILogSinkTest:TestSinkPrefixNamesTheCaller] ILog sink prefix probe
        var previous = ILog.Sink;
        try
        {
            ILog.Sink = new LoggingService();

            ILog.Print("ILog sink prefix probe");
        }
        finally
        {
            ILog.Sink = previous;
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
