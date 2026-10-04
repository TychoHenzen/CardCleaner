#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Entry for a tile within a biome pool - includes weight editing.
/// </summary>
[Tool]
public partial class BiomeTileEntry : HBoxContainer
{
    private readonly EditableTile? _tile;
    private readonly string? _biomeId;
    private readonly TileEditorService? _service;
    private readonly bool _isUniversal;
    private readonly float _weight;
    private readonly bool _isBlocked;

    public BiomeTileEntry() { }

    public BiomeTileEntry(
        EditableTile tile,
        string biomeId,
        TileEditorService service,
        bool isUniversal,
        float weight,
        bool isBlocked)
    {
        _tile = tile;
        _biomeId = biomeId;
        _service = service;
        _isUniversal = isUniversal;
        _weight = weight;
        _isBlocked = isBlocked;
    }

    public override void _Ready()
    {
        if (_tile == null || _service == null || _biomeId == null)
            return;

        CustomMinimumSize = new Vector2(280, 30);
        var indicator = new ColorRect
        {
            CustomMinimumSize = new Vector2(16, 16),
            Color = _tile.Passability.ToLowerInvariant() switch
            {
                "solid" => new Color(0.8f, 0.3f, 0.3f),
                "partially_passable" => new Color(0.8f, 0.8f, 0.3f),
                _ => new Color(0.3f, 0.8f, 0.3f)
            }
        };
        AddChild(indicator);

        var nameLabel = new Label
        {
            Text = _tile.Name.Length > 12 ? _tile.Name[..12] + ".." : _tile.Name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = $"{_tile.Id}\n{_tile.Name}\n({_tile.AtlasX}, {_tile.AtlasY})"
        };
        AddChild(nameLabel);
        AddWeightControl();
        AddRemoveControl();
    }

    private void AddWeightControl()
    {
        if (_isUniversal)
            return;

        var weightSpinBox = new SpinBox
        {
            MinValue = 0.01,
            MaxValue = 1.0,
            Step = 0.01,
            Value = _weight,
            CustomMinimumSize = new Vector2(70, 0),
            TooltipText = "Tile weight (will be normalized on save)"
        };
        weightSpinBox.ValueChanged += OnWeightChanged;
        AddChild(weightSpinBox);
    }

    private void AddRemoveControl()
    {
        if (_isUniversal)
            return;

        var removeButton = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(24, 24),
            TooltipText = "Remove from biome pool"
        };
        removeButton.Pressed += RemoveFromBiome;
        AddChild(removeButton);
    }

    private void OnWeightChanged(double newValue)
    {
        var biome = _service!.GetBiome(_biomeId!);
        if (biome == null)
            return;

        var pool = _isBlocked ? biome.BlockedTiles : biome.PassableTiles;
        if (!pool.ContainsKey(_tile!.Id))
            return;

        pool[_tile.Id] = (float)newValue;
        _service.UpdateBiome(biome);
    }

    private void RemoveFromBiome()
    {
        var biome = _service!.GetBiome(_biomeId!);
        if (biome == null)
            return;

        var pool = _isBlocked ? biome.BlockedTiles : biome.PassableTiles;
        if (pool.Remove(_tile!.Id))
            _service.UpdateBiome(biome);
    }
}
#endif
