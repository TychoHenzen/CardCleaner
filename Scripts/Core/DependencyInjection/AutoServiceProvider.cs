using System;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
/// Automatically discovers and registers services from child nodes with [Service] attribute.
/// Add this to any scene and it will recursively scan its children for services.
/// </summary>
public partial class AutoServiceProvider : Node, IServiceProvider
{
    [Export] public bool EnableDebugLogging { get; set; } = true;

    public void RegisterServices(IServiceContainer container)
    {
        var discoveredServices = 0;
        ScanNodeForServices(GetTree().CurrentScene, container, ref discoveredServices);
        
        if (EnableDebugLogging)
            ILog.Print($"AutoServiceProvider discovered {discoveredServices} services");
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");
    }

    private void ScanNodeForServices(Node node, IServiceContainer container, ref int discoveredCount)
    {
        // Check if this node is a service
        var nodeType = node.GetType();
        var serviceAttribute = nodeType.GetCustomAttributes(typeof(ServiceAttribute), false)
            .Cast<ServiceAttribute>()
            .FirstOrDefault();

        if (serviceAttribute != null)
        {
            RegisterNodeAsService(node, serviceAttribute, container);
            discoveredCount++;
        }

        // Recursively scan children
        foreach (Node child in node.GetChildren())
        {
            ScanNodeForServices(child, container, ref discoveredCount);
        }
    }

    private void RegisterNodeAsService(Node node, ServiceAttribute attribute, IServiceContainer container)
    {
        var nodeType = node.GetType();
        
        // Determine what types to register this service as
        var serviceTypes = attribute.ServiceTypes.Length > 0 
            ? attribute.ServiceTypes 
            : GetImplementedInterfaces(nodeType);

        foreach (var serviceType in serviceTypes)
        {
            RegisterServiceWithLifetime(container, serviceType, node, attribute.Lifetime, nodeType);
            
            if (EnableDebugLogging)
                ILog.Print($"Registered {nodeType.Name} as {serviceType.Name} ({attribute.Lifetime})");
        }

        // Also register as concrete type if not already included
        if (!serviceTypes.Contains(nodeType))
        {
            RegisterServiceWithLifetime(container, nodeType, node, attribute.Lifetime, nodeType);
        }
    }

    private static Type[] GetImplementedInterfaces(Type nodeType)
    {
        return nodeType.GetInterfaces()
            .Where(i => i != typeof(IServiceProvider) && !i.Name.StartsWith("Godot"))
            .ToArray();
    }

    private static void RegisterServiceWithLifetime(IServiceContainer container, Type serviceType, 
        Node instance, ServiceLifetime lifetime, Type implementationType)
    {
        switch (lifetime)
        {
            case ServiceLifetime.Singleton:
                container.RegisterSingleton(serviceType, instance);
                break;
                
            case ServiceLifetime.Transient:
                container.RegisterTransient(serviceType, implementationType);
                break;
                
            case ServiceLifetime.Scoped:
                // For future implementation
                throw new NotImplementedException("Scoped services not yet implemented");
                
            default:
                throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, null);
        }
    }
}