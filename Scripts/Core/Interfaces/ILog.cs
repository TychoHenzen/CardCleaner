using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface ILog
{
    void LogMessage(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogError(string message, Exception exception);

    public static void Print(string message)
    {
        if (ServiceLocator.Has<ILog>())
        {
            ServiceLocator.Get<ILog>().LogMessage(message);
        }
        else
        {
            GD.Print(message);
        }
    }
    public static void Warning(string message)
    {
        if (ServiceLocator.Has<ILog>())
        {
            ServiceLocator.Get<ILog>().LogWarning(message);
        }
        else
        {
            GD.PushWarning(message);
        }
    }
    public static void Error(string message)
    {
        if (ServiceLocator.Has<ILog>())
        {
            ServiceLocator.Get<ILog>().LogError(message);
        }
        else
        {
            GD.PushError(message);
        }
    }

    public static bool ExportCheck(Node? export, string name, Node target)
    {
        if (export != null) 
            return false;
        Error($"{name} not assigned to {target.Name}");
        return true;
    }
}