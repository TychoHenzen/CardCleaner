using System;
using CardCleaner.Scripts.Core.DependencyInjection;

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
            ServiceLocator.Get<ILog>(log => log.LogMessage(message));
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
            ServiceLocator.Get<ILog>(log => log.LogWarning(message));
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
            ServiceLocator.Get<ILog>(log => log.LogError(message));
        }
    }
}