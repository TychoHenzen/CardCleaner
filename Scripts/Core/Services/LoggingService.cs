using System;
using System.Diagnostics;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;


[Service]
public class LoggingService : ILog
{
    public void LogMessage(string message)
    {
        var prefix = GetCallerPrefix();
        Write($"[{prefix}] {message}", false);
    }

    public void LogWarning(string message)
    {
        var prefix = GetCallerPrefix();
        Write($"[{prefix}] WARNING: {message}", true);
    }

    public void LogError(string message)
    {
        var prefix = GetCallerPrefix();
        Write($"[{prefix}] ERROR: {message}", true);
    }

    public void LogError(string message, Exception exception)
    {
        var prefix = GetCallerPrefix();
        Write($"[{prefix}] ERROR: {message}\n{exception}", true);
    }

    // Called after GetCallerPrefix has returned, so overriding it does not change the frame the prefix names.
    protected virtual void Write(string line, bool isError)
    {
        if (isError)
            GD.PrintErr(line);
        else
            GD.Print(line);
    }

    private static string GetCallerPrefix()
    {
        var stackTrace = new StackTrace();
        var callerFrame = stackTrace.GetFrame(3); // Skip GetCallerPrefix, the LoggingService method and the ILog helper
        var caller = callerFrame?.GetMethod();
        return $"{caller?.DeclaringType?.Name ?? "Unknown"}:{caller?.Name ?? "Unknown"}";
    }
}