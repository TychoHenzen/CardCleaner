using System;
using CardCleaner.Scripts.Core.Enum;
using Godot;

[Tool]
[GlobalClass]
public partial class TilePattern : Resource
{
    [Export] public string PatternName { get; set; } = "";
    [Export] public float SpawnChance { get; set; } = 0.3f;
    [Export] public Vector2I Size { get; set; } = Vector2I.One; // Pattern dimensions
    [Export] public TileLayer Layer { get; set; } = TileLayer.Decoration;
    
    // Grid of tile placements - null means "don't place anything here"
    [Export] public TilePlacement[] Tiles { get; set; } = Array.Empty<TilePlacement>();
    
    // Get tile placement at specific position within pattern
    public TilePlacement GetTileAt(Vector2I localPos)
    {
        int index = localPos.Y * Size.X + localPos.X;
        if (index >= 0 && index < Tiles.Length)
            return Tiles[index];
        return null;
    }
}