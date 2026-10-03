using System;

namespace CardCleaner.Scripts.Core.DependencyInjection;

[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Singleton;
    public Type[] ServiceTypes { get; set; } = Array.Empty<Type>();

    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton, params Type[] serviceTypes)
    {
        Lifetime = lifetime;
        ServiceTypes = serviceTypes;
    }
}
