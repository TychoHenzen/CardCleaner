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
    public static void TestSinkPrefixNamesTheCaller()
    {
        var previous = ILog.Sink;
        var sink = new RecordingLoggingService();
        try
        {
            ILog.Sink = sink;

            ILog.Print("first");
            ILog.Warning("second");
            ILog.Error("third");

            Assertions.AssertThat(sink.Lines).IsEqual(new List<string>
            {
                "[ILogSinkTest:TestSinkPrefixNamesTheCaller] first",
                "[ILogSinkTest:TestSinkPrefixNamesTheCaller] WARNING: second",
                "[ILogSinkTest:TestSinkPrefixNamesTheCaller] ERROR: third",
            });
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    // The real LoggingService formatting and caller lookup, with the formatted lines recorded instead of printed.
    private sealed class RecordingLoggingService : LoggingService
    {
        public List<string> Lines { get; } = new();

        protected override void Write(string line, bool isError) => Lines.Add(line);
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
