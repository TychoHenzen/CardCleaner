using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

[Tool]
[GlobalClass]
public partial class TilePoolEntry : Resource
{
    // Default values as constants
    private const string DefaultTileId = "";
    private const float DefaultWeight = 1.0f;

    public TilePoolEntry() { }

    public TilePoolEntry(string tileId, float weight = DefaultWeight)
    {
        TileId = tileId;
        Weight = weight;
    }

    [Export] public string TileId { get; set; } = DefaultTileId;
    [Export] public float Weight { get; set; } = DefaultWeight;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileId) => true,
            nameof(Weight) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(TileId) => DefaultTileId,
            nameof(Weight) => DefaultWeight,
            _ => base._PropertyGetRevert(property)
        };
    }
}
