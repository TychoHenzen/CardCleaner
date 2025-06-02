using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.ServiceProviders;

/// <summary>
///     Service provider for game settings.
///     Add this node to any scene that needs configurable settings and add it to "service_providers" group.
///     Ensure a GameSettings node is a child of this provider.
/// </summary>
public partial class GameSettingsProvider : Node, IServiceProvider
{
    [Export] public GameSettings? GameSettings { get; set; }

    public void RegisterServices(IServiceContainer container)
    {
        if (GameSettings != null)
        {
            container.RegisterSingleton<IGameSettings>(GameSettings);
            ILog.Print("Registered game settings service");
        }
        else
        {
            ILog.Error("Cannot register GameSettings - node not found");
        }
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");

        if (GameSettings == null)
            ILog.Error("GameSettings node not found. Assign export.");
    }
}