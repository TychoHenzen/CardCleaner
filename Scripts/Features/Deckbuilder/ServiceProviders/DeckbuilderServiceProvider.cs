using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.ServiceProviders;

/// <summary>
///     Service provider for deckbuilder-related services.
///     Add this node to any scene that needs deckbuilder services and add it to "service_providers" group.
/// </summary>
public partial class DeckbuilderServiceProvider : Node, IServiceProvider
{
    [Export] public GameSessionService GameSessionService { get; set; }

    public void RegisterServices(IServiceContainer container)
    {
        if (GameSessionService != null)
        {
            container.RegisterSingleton<IGameSessionService>(GameSessionService);
            GD.Print("[DeckbuilderServiceProvider] Registered GameSessionService");
        }
        else
        {
            GD.PrintErr("[DeckbuilderServiceProvider] GameSessionService not assigned - skipping registration");
        }
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");
    }
}