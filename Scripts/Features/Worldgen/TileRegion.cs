using CardCleaner.Scripts.Core.Enum;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class TileRegion : Resource
{
    [Export] public string RegionName { get; set; } = "";
    [Export] public Vector2I RegionSize { get; set; } = Vector2I.One;
    [Export] public TilePlacement FillTile { get; set; }
    [Export] public float Weight { get; set; } = 1.0f;
    [Export] public TileLayer Layer { get; set; } = TileLayer.Decoration;
    [Export] public bool BlockEntireRegion { get; set; } = true;
    
    // Convert to TilePattern for use in spawning system
    public TilePattern ToTilePattern()
    {
        var tiles = new TilePlacement[RegionSize.X * RegionSize.Y];
        for (int i = 0; i < tiles.Length; i++)
        {
            tiles[i] = FillTile;
            tiles[i].BlocksTiles = BlockEntireRegion;
        }
        
        return new TilePattern
        {
            PatternName = RegionName,
            Weight = Weight,
            Size = RegionSize,
            Layer = Layer,
            Tiles = tiles,
        };
    }
}