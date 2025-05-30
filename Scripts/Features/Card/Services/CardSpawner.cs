using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services;

/// <summary>
///     Spawns Card instances one per frame at runtime when pressing 1, 2, or 3.
/// </summary>
public partial class CardSpawner : Node3D
{
    private ICardGenerator _generator;
    private IInputService _inputService;
    private RandomNumberGenerator _rng;

    private Node3D _spawnParent;
    private int _spawnQueue;
    [Export] public PackedScene CardScene { get; set; }
    [Export] public NodePath SpawnParentPath { get; set; }
    [Export] public Vector3 OffsetRange { get; set; } = Vector3.Zero;

    public override void _Ready()
    {
        _spawnParent = GetNode<Node3D>(SpawnParentPath);
        ServiceLocator.Get<RandomNumberGenerator>(rng => _rng = rng);
        ServiceLocator.Get<ICardGenerator>(gen => _generator = gen);
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
        if (_spawnQueue > 0)
        {
            _spawnQueue--;
            SpawnSingleCard(Models.CardSignature.Random(_rng));
        }
    }

    public override void _ExitTree()
    {
        // Clean up input registrations when component is destroyed
        _inputService?.UnregisterAllActions(this);
    }

    private void SpawnSingleCard(Models.CardSignature signature)
    {
        if (CardScene.Instantiate() is not Node3D cardInstance)
            return;
        _spawnParent.AddChild(cardInstance);
        var offset = new Vector3(
            (_rng.Randf() * 2 - 1) * OffsetRange.X,
            (_rng.Randf() * 2 - 1) * OffsetRange.Y,
            (_rng.Randf() * 2 - 1) * OffsetRange.Z
        );
        var transform = _spawnParent.GlobalTransform;
        transform.Origin += offset;
        cardInstance.GlobalTransform = transform;
        cardInstance.Name = "Card";

        if (cardInstance is CardController controller)
            controller.Signature = signature;
        var renderer = cardInstance.GetNode<CardShaderRenderer>("CardRenderer");
        if (renderer == null) return;
        CallDeferred(nameof(BakeCardRenderer), renderer, signature);
    }

    private void BakeCardRenderer(CardShaderRenderer renderer, Models.CardSignature signature)
    {
        var template = new Core.Data.CardTemplate();
        _generator.GenerateCardRenderer(renderer, signature, template);
        renderer.Bake(template);
    }
}