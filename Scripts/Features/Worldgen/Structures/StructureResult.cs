using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Structures;

/// <summary>
/// Result from procedural structure generation containing tiles and influence data.
/// </summary>
public sealed class StructureResult
{
    /// <summary>
    /// Tiles to place, keyed by offset from anchor position.
    /// </summary>
    public required IReadOnlyDictionary<Vector2I, string> Tiles { get; init; }

    /// <summary>
    /// Size of the generated structure (bounding box).
    /// </summary>
    public required Vector2I Size { get; init; }

    /// <summary>
    /// Radius of tile weight influence around the structure.
    /// </summary>
    public int InfluenceRadius { get; init; } = 3;

    /// <summary>
    /// Tile weight modifiers applied within the influence radius.
    /// </summary>
    public IReadOnlyList<TileAffinityEntry>? TileAffinities { get; init; }

    /// <summary>
    /// Create a result from a structure stamp (for consistent handling).
    /// </summary>
    public static StructureResult FromStamp(StructureStamp stamp)
    {
        return new StructureResult
        {
            Tiles = stamp.GetAllTiles(),
            Size = stamp.Size,
            InfluenceRadius = stamp.InfluenceRadius,
            TileAffinities = [.. stamp.TileAffinities]
        };
    }
}
