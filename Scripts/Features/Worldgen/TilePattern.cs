using System;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Enum;
using Godot;
[Tool]
[GlobalClass]
public partial class TilePattern : Resource
{
    [Export] public string PatternName { get; set; } = "";
    [Export] public float Weight { get; set; } = 1.0f; // Relative weight for this pattern
    [Export] public Vector2I Size { get; set; } = Vector2I.One;
    [Export] public TileLayer Layer { get; set; } = TileLayer.Decoration;
    
    [Export] public TilePlacement[] Tiles { get; set; } = Array.Empty<TilePlacement>();
    
    public TilePlacement? GetTileAt(Vector2I localPos)
    {
        int index = localPos.Y * Size.X + localPos.X;
        return index >= 0 && index < Tiles.Length ? Tiles[index] : null;
    }
    
    public bool ShouldBlockAt(Vector2I localPos)
    {
        int index = localPos.Y * Size.X + localPos.X;
        return Tiles[index].BlocksTiles;
    }
}