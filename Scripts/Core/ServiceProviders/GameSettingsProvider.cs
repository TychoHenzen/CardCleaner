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
    private GameSettings _gameSettings;
    [Export] public NodePath GameSettingsPath { get; set; } = "GameSettings";

    public void RegisterServices(IServiceContainer container)
    {
        if (_gameSettings != null)
        {
            container.RegisterSingleton<IGameSettings>(_gameSettings);
            GD.Print("[GameSettingsProvider] Registered game settings service");
        }
        else
        {
            GD.PrintErr("[GameSettingsProvider] Cannot register GameSettings - node not found");
        }
    }

    public override void _Ready()
    {
        AddToGroup("service_providers");
        _gameSettings = GetNode<GameSettings>(GameSettingsPath);

        if (_gameSettings == null)
            GD.PrintErr("[GameSettingsProvider] GameSettings node not found. Add a GameSettings child node.");
    }
}