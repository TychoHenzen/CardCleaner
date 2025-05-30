using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.DI;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services
{
    /// <summary>
    /// Service provider for card-related services.
    /// Add this node to any scene that needs card services and add it to "service_providers" group.
    /// </summary>
    public partial class CardServiceProvider : Node, IServiceProvider
    {
        [Export] public RarityVisual[] RarityVisuals { get; set; }
        [Export] public BaseCardType[] BaseCardTypes { get; set; }
        [Export] public GemVisual[] GemVisuals { get; set; }

        public override void _Ready()
        {
            AddToGroup("service_providers");
        }

        public void RegisterServices(IServiceContainer container)
        {
            // Only register if we have the required data
            if (RarityVisuals != null && BaseCardTypes != null && GemVisuals != null)
            {
                // Register the signature-based card generator
                container.RegisterSingleton<ICardGenerator>(
                    new SignatureCardGenerator(RarityVisuals, BaseCardTypes, GemVisuals));

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
        }
    }
}