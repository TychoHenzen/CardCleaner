using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     Base class for components that need dependency injection.
///     Handles automatic service resolution during Setup.
/// </summary>
public abstract partial class InjectableCardComponent : Node, ICardComponent
{
    protected bool _injected;

    public virtual void Setup(RigidBody3D cardRoot)
    {
        if (!_injected)
        {
            InjectServices();
            _injected = true;
        }

        OnSetup(cardRoot);
    }

    protected virtual void InjectServices()
    {
        // Override in derived classes to inject specific services
    }

    protected abstract void OnSetup(RigidBody3D cardRoot);

    protected T Inject<T>() where T : class
    {
        return ServiceLocator.Get<T>();
    }
}