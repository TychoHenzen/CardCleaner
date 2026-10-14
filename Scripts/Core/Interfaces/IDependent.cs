namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
///     A node that receives its services from the composition root instead of looking them up itself. The root calls
///     <see cref="Inject" /> when the node enters the scene tree: after the node's _EnterTree and before its _Ready
///     (children of an added subtree are injected the same way, each before its own _Ready). Inject may run again when
///     the node re-enters the tree, for example after a reparent. An implementation therefore only stores the resolver
///     in Inject and does its wiring in _Ready through the stored resolver, so the wiring runs once.
/// </summary>
public interface IDependent
{
    void Inject(IServiceResolver services);
}
