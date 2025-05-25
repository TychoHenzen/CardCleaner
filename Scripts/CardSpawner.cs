using Godot;
using CardCleaner.Scripts;

/// <summary>
/// Spawns Card instances one per frame at runtime when pressing 1, 2, or 3.
/// </summary>
public partial class CardSpawner : Node3D
{
    [Export] public PackedScene CardScene { get; set; }
    [Export] public NodePath SpawnParentPath { get; set; }
    [Export] public Vector3 OffsetRange { get; set; } = Vector3.Zero;

    [Export] public Texture2D[] CardBaseOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] BorderOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] CornerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] ArtOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] SymbolOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] ImageBackgroundOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] BannerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] DescriptionBoxOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyContainerOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFill1Options { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFill2Options { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] EnergyFillFullOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] GemSocketsOptions { get; set; } = new Texture2D[0];
    [Export] public Texture2D[] GemsOptions { get; set; } = new Texture2D[0];

    private Node3D _spawnParent;
    private RandomNumberGenerator _rng = new();
    private int _spawnQueue = 0;

    private void VerifyTextures(Texture2D[] list)
    {
        foreach (var texture in list)
        {
            if (texture.GetImage().GetFormat() != Image.Format.Rgba8)
            {
                GD.PrintErr($"Image {texture.ResourcePath} does not have argb8");
            }
        }
    }
    public override void _Ready()
    {
        _spawnParent = GetNode<Node3D>(SpawnParentPath);
        _rng.Randomize();
        
        VerifyTextures(CardBaseOptions);
        VerifyTextures(BorderOptions);
        VerifyTextures(CornerOptions);
        VerifyTextures(ArtOptions);
        VerifyTextures(SymbolOptions);
        VerifyTextures(ImageBackgroundOptions);
        VerifyTextures(BannerOptions);
        VerifyTextures(DescriptionBoxOptions);
        VerifyTextures(EnergyContainerOptions);
        VerifyTextures(EnergyFill1Options);
        VerifyTextures(EnergyFill2Options);
        VerifyTextures(EnergyFillFullOptions);
        VerifyTextures(GemSocketsOptions);
        VerifyTextures(GemsOptions);
        
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
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
            SpawnSingleCard();
            _spawnQueue--;
        }
    }

    private void SpawnSingleCard()
    {
        var cardInstance = CardScene.Instantiate() as Node3D;
        if (cardInstance == null)
            return;

        RandomizeCardRenderer(cardInstance);

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

    private static void Randomize(LayerData target, Texture2D[] textures, RandomNumberGenerator rng)
    {
        if (textures.Length > 1)
            target.Texture = textures[rng.RandiRange(0, textures.Length - 1)];
    }

    private void RandomizeCardRenderer(Node3D cardInstance)
    {
        var renderer = cardInstance.GetNode<CardShaderRenderer>("CardRenderer");
        if (renderer == null) return;

        Randomize(renderer.CardBase, CardBaseOptions, _rng);
        Randomize(renderer.Border, BorderOptions, _rng);
        Randomize(renderer.Corners, CornerOptions, _rng);

        Randomize(renderer.Art, ArtOptions, _rng);
        Randomize(renderer.Symbol, SymbolOptions, _rng);
        Randomize(renderer.ImageBackground, ImageBackgroundOptions, _rng);
        Randomize(renderer.Banner, BannerOptions, _rng);
        Randomize(renderer.DescriptionBox, DescriptionBoxOptions, _rng);
        Randomize(renderer.EnergyContainer, EnergyContainerOptions, _rng);

        if (_rng.Randf() > 0.5f)
        {
            Randomize(renderer.EnergyFill1, EnergyFill1Options, _rng);
            Randomize(renderer.EnergyFill2, EnergyFill2Options, _rng);
        }
        else
        {
            Randomize(renderer.EnergyFill1, EnergyFillFullOptions, _rng);
        }

        for (int i = 0; i < 8; i++)
        {
            Randomize(renderer.GemSockets[i], GemSocketsOptions, _rng);
            Randomize(renderer.Gems[i], GemsOptions, _rng);
        }
        renderer.Bake();
    }
}