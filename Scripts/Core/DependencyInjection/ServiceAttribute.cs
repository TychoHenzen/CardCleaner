using System;

namespace CardCleaner.Scripts.Core.DependencyInjection;

[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public Type[] ServiceTypes { get; set; } = Array.Empty<Type>();

    public ServiceAttribute(params Type[] serviceTypes)
    {
        ServiceTypes = serviceTypes;
    }
}
