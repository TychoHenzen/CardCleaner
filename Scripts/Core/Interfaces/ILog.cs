using System;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ILog
{
    /// <summary>
    /// Logger that the static helpers below write to. ServiceLocator sets it to the ILog it registers and clears it
    /// on reset or replacement. While it is null, the helpers fall back to GD output.
    /// </summary>
    public static ILog? Sink { get; set; }

    void LogMessage(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception exception);

    // ASSUMPTION: LoggingService.GetCallerPrefix reads stack frame 3, so each helper must call the sink directly.
    public static void Print(string message)
    {
        var sink = Sink;
        if (sink != null)
            sink.LogMessage(message);
        else
            GD.Print(message);
    }

    public static void Warning(string message)
    {
        var sink = Sink;
        if (sink != null)
            sink.LogWarning(message);
        else
            GD.PushWarning(message);
    }

    public static void Error(string message)
    {
        var sink = Sink;
        if (sink != null)
            sink.LogError(message);
        else
            GD.PushError(message);
    }

    public static bool ExportCheck(Node? export, string name, Node target)
    {
        if (export != null)
            return false;
        Error($"{name} not assigned to {target.Name}");
        return true;
    }
}