using System;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>Fills a node's service property from the resolver unless it was assigned directly (tests do).</summary>
internal static class ServiceFill
{
    public static void Into<T>(IServiceResolver services, Func<T?> current, Action<T> assign) where T : class =>
        services.Get<T>(service =>
        {
            if (current() == null)
                assign(service);
        });
}
