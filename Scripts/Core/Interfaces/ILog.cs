using System;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ILog
{
    /// <summary>
    /// Logger the static helpers write to, set by the composition root; only TestServices.Reset clears it.
    /// While it is null, the helpers write to the console.
    /// </summary>
    public static ILog? Sink { get; set; }

    void LogMessage(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception exception);

    // ASSUMPTION: LoggingService.GetCallerPrefix reads stack frame 3, so Print, Warning and Error each call the sink
    // directly; NodeExports.ExportCheck goes through Error, so its lines name NodeExports:ExportCheck.
    public static void Print(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            Console.Out.WriteLine(message);
            return;
        }

        sink.LogMessage(message);
    }

    public static void Warning(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            Console.Out.WriteLine(message);
            return;
        }

        sink.LogWarning(message);
    }

    public static void Error(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            Console.Error.WriteLine(message);
            return;
        }

        sink.LogError(message);
    }
}
