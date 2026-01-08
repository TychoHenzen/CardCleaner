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
    public TileDefinition(
        string id,
        string name,
        TilePassability passability,
        Vector2I atlasCoords,
        int sourceId = 4,
        TileLayer layer = TileLayer.Terrain,
        float elevation = 0f,
        bool? isTransparent = null,
        HashSet<string>? allowedBiomes = null,
        Vector2I? size = null,
        float decorationDensity = 1.0f,
        Vector2I?[]? autoTileVariants = null,
        string autoTileFormatName = "corner16",
        Vector2I[]? variations = null,
        VariationMode variationMode = VariationMode.PerInstance,
        TileAnimation? animation = null,
        int dominance = 0,
        string? innerTerrainId = null,
        string? outerTerrainId = null,
        bool isGapTile = false)
    {
        Id = id;
        Name = name;
        Passability = passability;
        AtlasCoords = atlasCoords;
        SourceId = sourceId;
        Layer = layer;
        Elevation = elevation;
        IsTransparent = isTransparent ?? (passability == TilePassability.Passable);
        AllowedBiomes = allowedBiomes;
        Size = size ?? Vector2I.One;
        DecorationDensity = decorationDensity;
        AutoTileVariants = autoTileVariants;
        AutoTileFormatName = autoTileFormatName;
        Variations = variations;
        VariationMode = variationMode;
        Animation = animation;
        Dominance = dominance;
        InnerTerrainId = innerTerrainId;
        OuterTerrainId = outerTerrainId;
        IsGapTile = isGapTile;
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
    /// Auto-tile variant atlas coordinates indexed by bitmask.
    /// For Corner16 format: 16 entries indexed by 4-bit corner mask (0-15).
    /// For Blob47 format: 47 entries indexed by GetBlobIndex().
    /// Null array means no auto-tiling. Null elements use the base tile's atlas coords.
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
    /// Visual variations for this tile (different atlas coordinates for the same tile type).
    /// Null means no variations (always use base AtlasCoords).
    /// </summary>
    public Vector2I[]? Variations { get; }

    /// <summary>
    /// How variations are selected during map generation.
    /// PerInstance: Random per tile placement (foliage-style).
    /// PerGeneration: One variant selected at generation start, used for all instances.
    /// </summary>
    public VariationMode VariationMode { get; }

    /// <summary>
    /// Animation configuration for this tile. Null means no animation.
    /// </summary>
    public TileAnimation? Animation { get; }

    /// <summary>
    /// Visual dominance for terrain transitions. Higher values render on top of lower values.
    /// Used by dual-grid auto-tiling to determine which terrain's edges show at boundaries.
    /// Defaults to file order index if not specified in tile definition.
    /// </summary>
    public int Dominance { get; }

    /// <summary>
    /// For auto-tiles: the terrain whose border is shown (higher dominance terrain).
    /// Null means use dominance-based resolution at runtime.
    /// </summary>
    public string? InnerTerrainId { get; }

    /// <summary>
    /// For auto-tiles: the background terrain.
    /// "*" = compositable (transparent border, composite onto any base terrain).
    /// Specific tile ID = fixed transition (baked pair, use as-is).
    /// Null = use dominance-based resolution at runtime.
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

        int index;
        int maxIndex;

        var format = GetAutoTileFormat();
        if (format?.BitmaskType == BitmaskType.Full8)
        {
            // For blob format, convert 8-bit mask to 0-46 index
            index = NeighborBitmask8.GetBlobIndex(bitmask);
            maxIndex = 47;
        }
        else
        {
            // For corner16 and edge16 formats, use mask directly as index (both are 4-bit, 0-15)
            index = bitmask;
            maxIndex = 16;
        }

        if (index < 0 || index >= maxIndex || index >= AutoTileVariants.Length)
            return AtlasCoords;

        return AutoTileVariants[index] ?? AtlasCoords;
    }
}
