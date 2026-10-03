using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Animation configuration for animated tiles.
/// </summary>
public record TileAnimation(
    Vector2I[] Frames,
    float FrameDuration = 0.2f)
{
    /// <summary>
    /// Frame atlas coordinates. First frame is typically the base AtlasCoords.
    /// </summary>
    public Vector2I[] Frames { get; init; } = Frames;

    /// <summary>
    /// Duration of each frame in seconds.
    /// </summary>
    public float FrameDuration { get; init; } = FrameDuration;

    /// <summary>
    /// Total animation duration in seconds.
    /// </summary>
    public float TotalDuration => Frames.Length * FrameDuration;
}

/// <summary>
/// Defines a tile type with all its properties
/// </summary>
public class TileDefinition
{
    public TileDefinition(string id, string name, TilePassability passability, Vector2I atlasCoords)
        : this(id, name, passability, atlasCoords, new TileDefinitionOptions())
    {
    }

    internal TileDefinition(
        string id,
        string name,
        TilePassability passability,
        Vector2I atlasCoords,
        TileDefinitionOptions options)
    {
        Id = id;
        Name = name;
        Passability = passability;
        AtlasCoords = atlasCoords;
        SourceId = options.SourceId;
        Layer = options.Layer;
        Elevation = options.Elevation;
        IsTransparent = options.IsTransparent ?? (passability == TilePassability.Passable);
        AllowedBiomes = options.AllowedBiomes;
        Size = options.Size ?? Vector2I.One;
        DecorationDensity = options.DecorationDensity;
        AutoTileVariants = options.AutoTileVariants;
        AutoTileFormatName = options.AutoTileFormatName;
        Variations = options.Variations;
        VariationMode = options.VariationMode;
        Animation = options.Animation;
        Dominance = options.Dominance;
        InnerTerrainId = options.InnerTerrainId;
        OuterTerrainId = options.OuterTerrainId;
        IsGapTile = options.IsGapTile;
        Probability = options.Probability;
    }

    public string Id { get; }
    public string Name { get; }
    public TilePassability Passability { get; }
    public Vector2I AtlasCoords { get; }
    public int SourceId { get; }
    public TileLayer Layer { get; }
    public float Elevation { get; }
    public bool IsTransparent { get; }
    public HashSet<string>? AllowedBiomes { get; }

    /// <summary>
    /// Size of this tile in grid cells (width, height). Default is (1, 1).
    /// Multi-cell tiles like trees may be 2x1, 2x2, etc.
    /// </summary>
    public Vector2I Size { get; }

    /// <summary>
    /// Probability (0.0-1.0) of this decoration tile appearing on valid positions.
    /// Only meaningful for decoration layer tiles. Default 1.0 = 100% coverage.
    /// </summary>
    public float DecorationDensity { get; }

    /// <summary>
    /// Auto-tile variant atlas coordinates indexed by bitmask; null entries use the base atlas coordinates.
    /// </summary>
    public Vector2I?[]? AutoTileVariants { get; }

    /// <summary>
    /// The name of the auto-tile format used by this tile (e.g., "corner16", "edge16", "blob47").
    /// Resolved via <see cref="AutoTileFormatRegistry"/> at runtime.
    /// </summary>
    public string AutoTileFormatName { get; }

    /// <summary>
    /// Gets the auto-tile format definition from the registry.
    /// Returns null if the format is not registered.
    /// </summary>
    public AutoTileFormatDefinition? GetAutoTileFormat()
    {
        return AutoTileFormatRegistry.TryGet(AutoTileFormatName, out var format) ? format : null;
    }

    /// <summary>
    /// Gets the variant definition for a specific bitmask value.
    /// Returns null if the format is not found or the bitmask is not allowed.
    /// </summary>
    /// <param name="bitmask">The bitmask value (0-15 for 4-bit formats, 0-255 for 8-bit).</param>
    /// <returns>The variant definition, or null if not found.</returns>
    public VariantDefinition? GetVariantDefinition(int bitmask)
    {
        var format = GetAutoTileFormat();
        return format?.GetVariant(bitmask);
    }

    /// <summary>
    /// Visual variations for this tile; null means the base AtlasCoords is always used.
    /// </summary>
    public Vector2I[]? Variations { get; }

    /// <summary>
    /// How variations are selected during map generation, per instance or per generation.
    /// </summary>
    public VariationMode VariationMode { get; }

    /// <summary>
    /// Animation configuration for this tile. Null means no animation.
    /// </summary>
    public TileAnimation? Animation { get; }

    /// <summary>
    /// Visual dominance for terrain transitions; higher values render on top of lower values.
    /// </summary>
    public int Dominance { get; }

    /// <summary>
    /// For auto-tiles: the terrain whose border is shown (higher dominance terrain).
    /// Null means use dominance-based resolution at runtime.
    /// </summary>
    public string? InnerTerrainId { get; }

    /// <summary>
    /// For auto-tiles, identifies the background terrain: "*" is compositable, a tile ID fixes the transition,
    /// and null uses dominance-based resolution.
    /// </summary>
    public string? OuterTerrainId { get; }

    /// <summary>
    /// Returns true if this auto-tile is compositable (OuterTerrainId is "*"),
    /// meaning its border should be composited onto any base terrain at atlas compile time.
    /// </summary>
    public bool IsCompositable => OuterTerrainId == "*";

    /// <summary>
    /// Returns true if this auto-tile has a fixed transition (OuterTerrainId is a specific tile ID),
    /// meaning it has a baked background and should be used as-is.
    /// </summary>
    public bool IsFixedTransition => !string.IsNullOrEmpty(OuterTerrainId) && OuterTerrainId != "*";

    /// <summary>
    /// Whether this tile has visual variations.
    /// </summary>
    public bool HasVariations => Variations != null && Variations.Length > 0;

    /// <summary>
    /// Whether this tile has animation frames.
    /// </summary>
    public bool HasAnimation => Animation != null && Animation.Frames.Length > 1;

    /// <summary>
    /// Expected number of auto-tile variants based on format.
    /// Delegates to the format definition from the registry.
    /// </summary>
    public int ExpectedVariantCount => GetAutoTileFormat()?.GetExpectedVariantCount() ?? 16;

    /// <summary>
    /// Whether this tile is a gap tile - used to fill gaps between auto-tiles.
    /// Gap tiles are non-auto-tile terrain tiles that separate different auto-tile regions.
    /// </summary>
    public bool IsGapTile { get; }

    /// <summary>
    /// Selection probability/weight for this tile; in variation groups, it controls density and relative weight.
    /// </summary>
    public float Probability { get; }

    /// <summary>
    /// Whether this is a simple terrain tile (no auto-tile variants, terrain layer).
    /// Simple terrain tiles can serve as base backgrounds for compositable auto-tiles.
    /// </summary>
    public bool IsSimpleTerrain => !HasAutoTileVariants && Layer == TileLayer.Terrain;

    /// <summary>
    /// Whether this is an auto-tile (has auto-tile variants).
    /// </summary>
    public bool IsAutoTile => HasAutoTileVariants;

    /// <summary>
    /// Whether this is a decoration tile.
    /// </summary>
    public bool IsDecoration => Layer == TileLayer.Decoration;

    /// <summary>
    /// Whether this tile is solid (impassable).
    /// </summary>
    public bool IsSolid => Passability == TilePassability.Solid;

    public bool IsPassable => Passability == TilePassability.Passable;
    public bool HasAutoTileVariants => AutoTileVariants != null;

    public bool IsAllowedInBiome(string biomeId) => AllowedBiomes == null || AllowedBiomes.Contains(biomeId);

    /// <summary>
    /// Check if this decoration tile should appear based on its density probability.
    /// </summary>
    /// <param name="rng">Random number generator</param>
    /// <returns>True if decoration should appear, false to skip</returns>
    public bool ShouldPlaceDecoration(RandomNumberGenerator rng)
    {
        if (Layer != TileLayer.Decoration) return true;
        return rng.Randf() <= DecorationDensity;
    }

    /// <summary>
    /// Get the atlas coordinates for a specific auto-tile bitmask.
    /// For Corner16 format: bitmask is 0-15 (4-bit corner mask).
    /// For Blob47 format: bitmask is the raw 8-bit value, converted to index internally.
    /// Returns base atlas coords if no variant is defined for that bitmask.
    /// </summary>
    public Vector2I GetAutoTileCoords(int bitmask)
    {
        if (AutoTileVariants == null)
            return AtlasCoords;

        // All auto-tiles use Corner16 format (16 variants indexed 0-15)
        // Dual-grid only samples 4 corners, so this is the native format
        if (bitmask < 0 || bitmask >= AutoTileVariants.Length)
            return AtlasCoords;

        return AutoTileVariants[bitmask] ?? AtlasCoords;
    }
}
