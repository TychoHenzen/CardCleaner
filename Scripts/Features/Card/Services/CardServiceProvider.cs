using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Service provider for card-related services.
///     Add this node to any scene that needs card services and add it to "service_providers" group.
/// </summary>
public partial class CardServiceProvider : Node, IServiceProvider
{
    [Export] public required RarityVisual[] RarityVisuals { get; set; }
    [Export] public required BaseCardType[] BaseCardTypes { get; set; }
    [Export] public required GemVisual[] GemVisuals { get; set; }
    [Export] public required CardSpawner CardRoot { get; set; }

    public void RegisterServices(IServiceContainer container)
    {
        // Only register if we have the required data
        // Register individual visual configurations
        container.RegisterSingleton(RarityVisuals);
        container.RegisterSingleton(BaseCardTypes);
        container.RegisterSingleton(GemVisuals);
        container.RegisterSingleton<ICardGenerator,SignatureCardGenerator>();
        container.RegisterSingleton<ICardSpawner>(CardRoot);
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");
    }
}