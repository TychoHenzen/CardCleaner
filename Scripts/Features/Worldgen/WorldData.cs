using System.Linq;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen;

[Tool]
[GlobalClass]
public partial class WorldData : Resource
{
    // Default values as constants
    private const int DefaultTileSize = 16;

    public Array<SemanticTile> SemanticTiles => new(TerrainTiles
        .Union(StructureTiles).Union(DecorTiles).Union(EffectTiles));

    [Export] public Array<SemanticTile> TerrainTiles { get; set; } = new();
    [Export] public Array<SemanticTile> StructureTiles { get; set; } = new();
    [Export] public Array<SemanticTile> DecorTiles { get; set; } = new();
    [Export] public Array<SemanticTile> EffectTiles { get; set; } = new();
    [Export] public Array<EnemySpawnData> EnemySpawnData { get; set; } = new();
    [Export] public int TileSize { get; set; } = DefaultTileSize;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileSize) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileSize) => DefaultTileSize,
            _ => base._PropertyGetRevert(property)
        };
    }
}