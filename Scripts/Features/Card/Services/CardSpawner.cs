using Godot;
using CardCleaner.Scripts;
using CardCleaner.Scripts.Controllers;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.DI;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Interfaces;

/// <summary>
/// Spawns Card instances one per frame at runtime when pressing 1, 2, or 3.
/// </summary>
public partial class CardSpawner : Node3D
{
    [Export] public PackedScene CardScene { get; set; }
    [Export] public NodePath SpawnParentPath { get; set; }
    [Export] public Vector3 OffsetRange { get; set; } = Vector3.Zero;

    [Export] public TextureLoader Card { get; set; }

    private Node3D _spawnParent;
    private int _spawnQueue = 0;
    private ICardGenerator _generator;

    private RandomNumberGenerator _rng;

    public override void _Ready()
    {
        _spawnParent = GetNode<Node3D>(SpawnParentPath);
        ServiceLocator.Get<RandomNumberGenerator>(rng => _rng = rng);
        ServiceLocator.Get<ICardGenerator>(gen => _generator = gen);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            int count = keyEvent.Keycode switch
            {
                Key.Key1 => 1,
                Key.Key2 => 10,
                Key.Key3 => 100,
                _ => 0
            };
            if (count > 0)
            {
                _spawnQueue += count;
                GD.Print($"Queued {count} card(s). {_spawnQueue} remaining.");
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_spawnQueue > 0)
        {
            _spawnQueue--;
            SpawnSingleCard(CardSignature.Random(_rng));
        }
    }

    private void SpawnSingleCard(CardSignature signature)
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

    private void BakeCardRenderer(CardShaderRenderer renderer, CardSignature signature)
    {
        _generator.GenerateCardRenderer(renderer, signature);
        renderer.Bake();
    }
}