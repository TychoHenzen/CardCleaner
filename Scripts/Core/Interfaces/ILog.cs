using System;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ILog
{
    /// <summary>
    /// Logger that the static helpers below write to. The composition root sets it to the ILog it registers and
    /// clears it when its services are reset. While it is null, the helpers fall back to GD output.
    /// </summary>
    public static ILog? Sink { get; set; }

    void LogMessage(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception exception);

    // ASSUMPTION: LoggingService.GetCallerPrefix reads stack frame 3, so Print, Warning and Error each call the sink
    // directly. ExportCheck goes through Error, so its lines name ILog:ExportCheck, as they did before the sink.
    public static void Print(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            GD.Print(message);
            return;
        }

        sink.LogMessage(message);
    }

    public static void Warning(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            GD.PushWarning(message);
            return;
        }

        sink.LogWarning(message);
    }

    public static void Error(string message)
    {
        var sink = Sink;
        if (sink == null)
        {
            GD.PushError(message);
            return;
        }

        sink.LogError(message);
    }

    public static bool ExportCheck(Node? export, string name, Node target)
    {
        if (export != null)
            return false;
        Error($"{name} not assigned to {target.Name}");
        return true;
    }
}