using Godot;
using CardCleaner.Scripts;
using CardCleaner.Scripts.Controllers;
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
    
    [Export] public RarityVisual[] RarityVisuals { get; set; }
    [Export] public BaseCardType[] BaseCardTypes { get; set; }
    [Export] public GemVisual[] GemVisuals { get; set; }

    private Node3D _spawnParent;
    private int _spawnQueue = 0;
    private ICardGenerator _generator;

    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _spawnParent = GetNode<Node3D>(SpawnParentPath);
        
        CallDeferred(nameof(DeferredAssign));
    }

    public void DeferredAssign()
    {
        _generator = new SignatureCardGenerator(RarityVisuals, BaseCardTypes, GemVisuals);
        // _generator = new CardRandomizer(_rng, new CardTemplate(Card));
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
            SpawnSingleCard(CardSignature.Random(_rng));
            _spawnQueue--;
        }
    }

    private void SpawnSingleCard(CardSignature signature)
    {
        if (CardScene.Instantiate() is not Node3D cardInstance)
            return;
        var renderer = cardInstance.GetNode<CardShaderRenderer>("CardRenderer");
        if (renderer == null) return;

        _generator.GenerateCardRenderer(renderer, signature);
        renderer.Bake();
        cardInstance.GetNode<CardController>("Card").Signature = signature;

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
    }

}