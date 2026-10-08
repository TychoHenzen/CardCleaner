using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Spawns Card instances one per frame at runtime when pressing 1, 2, or 3, and one special card (magical
///     potential, which opens the backoffice seam) when pressing 4, unless <see cref="SpawnKeys" /> is off.
/// </summary>
[Service(ServiceLifetime.Singleton, typeof(ICardSpawner))]
public partial class CardSpawner : Node3D, ICardSpawner
{
    // Default values as constants
    private static readonly Vector3 DefaultOffsetRange = Vector3.Zero;

    private ICardSpawningService? _spawningService;
    private IInputService? _inputService;
    private RandomNumberGenerator? _rng;

    private int _spawnQueue;
    [Export] public Vector3 OffsetRange { get; set; } = DefaultOffsetRange;

    /// <summary>Whether keys 1 to 4 spawn cards. A scene that lays out its own cards switches them off.</summary>
    [Export] public bool SpawnKeys { get; set; } = true;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(OffsetRange) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(OffsetRange) => Variant.From(DefaultOffsetRange),
            _ => base._PropertyGetRevert(property)
        };
    }

    public override void _Ready()
    {
        ServiceLocator.Get<ICardSpawningService>(service => _spawningService = service);
        ServiceLocator.Get<RandomNumberGenerator>(rng => _rng = rng);
        if (!SpawnKeys) return;

        ServiceLocator.Get<IInputService>(input =>
        {
            input.RegisterAction(this, "spawn_one", Key.Key1, () => QueueCards(1));
            input.RegisterAction(this, "spawn_ten", Key.Key2, () => QueueCards(10));
            input.RegisterAction(this, "spawn_hundred", Key.Key3, () => QueueCards(100));
            input.RegisterAction(this, "spawn_special", Key.Key4, () => SpawnSpecialCard());
            _inputService = input;
        });
    }

    /// <summary>
    ///     Spawns one card with magical potential, so the backoffice seam can be tried without opening
    ///     512 cards. Returns null until the spawning service and the random source are available.
    /// </summary>
    public Node3D? SpawnSpecialCard()
    {
        return _rng == null ? null : SpawnSpecificCard(CardSignature.RandomSpecial(_rng));
    }

    private void QueueCards(int count)
    {
        _spawnQueue += count;
        ILog.Print($"Queued {count} card(s). {_spawnQueue} remaining.");
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
        if (_spawningService is null)
        {
            CallDeferred(MethodName.SpawnSingleCard);
            return;
        }

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
    public Node3D? SpawnSpecificCard(CardSignature signature, Vector3? position = null)
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