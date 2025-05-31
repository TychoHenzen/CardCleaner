using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Spawns Card instances one per frame at runtime when pressing 1, 2, or 3.
/// </summary>
public partial class CardSpawner : Node3D, ICardSpawner
{
    private ICardSpawningService _spawningService;
    private IInputService _inputService;

    private int _spawnQueue;
    [Export] public Vector3 OffsetRange { get; set; } = Vector3.Zero;

    public override void _Ready()
    {
        ServiceLocator.Get<ICardSpawningService>(service => _spawningService = service);
        ServiceLocator.Get<IInputService>(input =>
        {
            input.RegisterAction("spawn_one", Key.Key1, () => QueueCards(1));
            input.RegisterAction("spawn_ten", Key.Key2, () => QueueCards(10));
            input.RegisterAction("spawn_hundred", Key.Key3, () => QueueCards(100));
            _inputService = input;
        });
    }

    private void QueueCards(int count)
    {
        _spawnQueue += count;
        GD.Print($"Queued {count} card(s). {_spawnQueue} remaining.");
    }

    public override void _Process(double delta)
    {
        if (_spawnQueue <= 0) return;
        _spawnQueue--;
        SpawnSingleCard();
    }

    public override void _ExitTree()
    {
        // Clean up input registrations when component is destroyed
        _inputService?.UnregisterAllActions(this);
    }

    private void SpawnSingleCard()
    {
        // Calculate spawn transform with random offset
        var offset = _spawningService.GetRandomOffset(OffsetRange);
        var spawnTransform = GlobalTransform;
        spawnTransform.Origin += offset;

        // Use the spawning service to handle all the complex spawning logic
        _spawningService.SpawnRandomCard(spawnTransform, this);
    }
    
    /// <summary>
    /// Public method to spawn a card with a specific signature (useful for other systems)
    /// </summary>
    /// <param name="signature">The card signature to spawn</param>
    /// <param name="position">Optional world position, defaults to spawn parent position</param>
    public Node3D SpawnSpecificCard(CardSignature signature, Vector3? position = null)
    {
        if (_spawningService == null) return null;

        var spawnTransform = GlobalTransform;
        if (position.HasValue)
            spawnTransform.Origin = position.Value;

        return _spawningService.SpawnCard(signature, spawnTransform, this);
    }

    public Node3D GetNode()
    {
        return this;
    }
}
