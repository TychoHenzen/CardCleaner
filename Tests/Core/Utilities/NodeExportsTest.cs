using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using Godot;

namespace CardCleaner.Tests.Core.Utilities;

[TestSuite]
[RequireGodotRuntime]
public class NodeExportsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestASetExportReturnsFalseAndLogsNothing()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        var export = new Node();
        var target = new Node { Name = "Target" };
        try
        {
            ILog.Sink = sink;

            var missing = NodeExports.ExportCheck(export, "Camera", target);

            Assertions.AssertBool(missing).IsFalse();
            Assertions.AssertThat(sink.Errors).IsEmpty();
        }
        finally
        {
            ILog.Sink = previous;
            export.Free();
            target.Free();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestANullExportReturnsTrueAndLogsTheNameAndTarget()
    {
        var sink = new RecordingLog();
        var previous = ILog.Sink;
        var target = new Node { Name = "Target" };
        try
        {
            ILog.Sink = sink;

            var missing = NodeExports.ExportCheck(null, "Camera", target);

            Assertions.AssertBool(missing).IsTrue();
            Assertions.AssertThat(sink.Errors).IsEqual(new List<string> { "Camera not assigned to Target" });
        }
        finally
        {
            ILog.Sink = previous;
            target.Free();
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }
}
