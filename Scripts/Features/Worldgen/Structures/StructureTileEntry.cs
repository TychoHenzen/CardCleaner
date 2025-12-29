using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Single tile entry in a structure stamp, mapping an offset to a tile ID.
/// </summary>
[Tool]
[GlobalClass]
public partial class StructureTileEntry : Resource
{
    private const string DefaultTileId = "";
    private static readonly Vector2I DefaultOffset = Vector2I.Zero;

    public StructureTileEntry() { }

    public StructureTileEntry(Vector2I offset, string tileId)
    {
        Offset = offset;
        TileId = tileId;
    }

    /// <summary>Offset from the stamp's anchor position (top-left).</summary>
    [Export] public Vector2I Offset { get; set; } = DefaultOffset;

    /// <summary>The tile ID to place at this offset.</summary>
    [Export] public string TileId { get; set; } = DefaultTileId;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Offset) => true,
            nameof(TileId) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Offset) => DefaultOffset,
            nameof(TileId) => DefaultTileId,
            _ => base._PropertyGetRevert(property)
        };
    }
}
