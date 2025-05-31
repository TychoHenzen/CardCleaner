using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Scripts.Core.ServiceProviders;

/// <summary>
///     Service provider for card-related services.
///     Add this node to any scene that needs card services and add it to "service_providers" group.
/// </summary>
public partial class CardServiceProvider : Node, IServiceProvider
{
    [Export] public RarityVisual[] RarityVisuals { get; set; }
    [Export] public BaseCardType[] BaseCardTypes { get; set; }
    [Export] public GemVisual[] GemVisuals { get; set; }
    [Export] public CardSpawningService SpawningService { get; set; }
    [Export] public CardSpawner CardRoot { get; set; }

    public void RegisterServices(IServiceContainer container)
    {
        // Only register if we have the required data
        if (RarityVisuals != null && BaseCardTypes != null && GemVisuals != null)
        {
            // Register individual visual configurations
            container.RegisterSingleton(RarityVisuals);
            container.RegisterSingleton(BaseCardTypes);
            container.RegisterSingleton(GemVisuals);

            GD.Print("[CardServiceProvider] Registered card generation services");
        }
        else
        {
            GD.PrintErr("[CardServiceProvider] Missing required visual data for service registration");
        }
        container.RegisterSingleton<ICardGenerator,SignatureCardGenerator>();
        container.RegisterSingleton<ICardSpawner>(CardRoot);
        
        if (SpawningService != null)
        {
            container.RegisterSingleton<ICardSpawningService>(SpawningService);
            GD.Print("[CardServiceProvider] Registered SpawningService");
        }
        else
        {
            GD.PrintErr("[CardServiceProvider] SpawningService not assigned - skipping registration");
        }
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");
    }
}