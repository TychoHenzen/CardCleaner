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
    // Default values as constants
    private const bool DefaultEnableDebugLogging = true;

    [Export] public bool EnableDebugLogging { get; set; } = DefaultEnableDebugLogging;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EnableDebugLogging) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EnableDebugLogging) => DefaultEnableDebugLogging,
            _ => base._PropertyGetRevert(property)
        };
    }

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
            container.RegisterSingleton(serviceType, node);

            if (EnableDebugLogging)
                ILog.Print($"Registered {nodeType.Name} as {serviceType.Name}");
        }

        // Also register as concrete type if not already included
        if (!serviceTypes.Contains(nodeType))
        {
            container.RegisterSingleton(nodeType, node);
        }
    }

    private static Type[] GetImplementedInterfaces(Type nodeType)
    {
        return nodeType.GetInterfaces()
            .Where(i => i != typeof(IServiceProvider) && !i.Name.StartsWith("Godot"))
            .ToArray();
    }
}