using System;
using System.Diagnostics;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class LoggingService : ILog
{
    public void LogMessage(string message)
    {
        var prefix = GetCallerPrefix();
        GD.Print($"[{prefix}] {message}");
    }

    public void LogWarning(string message)
    {
        var prefix = GetCallerPrefix();
        GD.PrintErr($"[{prefix}] WARNING: {message}");
    }

    public void LogError(string message)
    {
        var prefix = GetCallerPrefix();
        GD.PrintErr($"[{prefix}] ERROR: {message}");
    }

    public void LogError(string message, Exception exception)
    {
        var prefix = GetCallerPrefix();
        GD.PrintErr($"[{prefix}] ERROR: {message}\n{exception}");
    }

    private static string GetCallerPrefix()
    {
        var stackTrace = new StackTrace();
        var callerFrame = stackTrace.GetFrame(3); // Skip LoggingService method and ILoggingService method
        var caller = callerFrame?.GetMethod();
        return $"{caller?.DeclaringType?.Name ?? "Unknown"}:{caller?.Name ?? "Unknown"}";
    }
}