using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

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
        HashSet<BiomeType>? allowedBiomes = null,
        Vector2I? size = null,
        float decorationDensity = 1.0f,
        BlobGenerationConfig? blobSettings = null,
        Vector2I?[]? autoTileVariants = null,
        AutoTileFormat autoTileFormat = AutoTileFormat.Corner16)
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
        BlobSettings = blobSettings;
        AutoTileVariants = autoTileVariants;
        AutoTileFormat = autoTileFormat;
    }

    public string Id { get; }
    public string Name { get; }
    public TilePassability Passability { get; }
    public Vector2I AtlasCoords { get; }
    public int SourceId { get; }
    public TileLayer Layer { get; }
    public float Elevation { get; }
    public bool IsTransparent { get; }
    public HashSet<BiomeType>? AllowedBiomes { get; }

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
    /// Per-tile blob generation settings. Null means use global defaults.
    /// </summary>
    public BlobGenerationConfig? BlobSettings { get; }

    /// <summary>
    /// Auto-tile variant atlas coordinates indexed by bitmask.
    /// For Corner16 format: 16 entries indexed by 4-bit corner mask (0-15).
    /// For Blob47 format: 47 entries indexed by GetBlobIndex().
    /// Null array means no auto-tiling. Null elements use the base tile's atlas coords.
    /// </summary>
    public Vector2I?[]? AutoTileVariants { get; }

    /// <summary>
    /// The auto-tile format used by this tile.
    /// Corner16 = 4-bit diagonal corners (16 variants).
    /// Blob47 = 8-bit edges+corners with constraint (47 variants).
    /// </summary>
    public AutoTileFormat AutoTileFormat { get; }

    /// <summary>
    /// Expected number of auto-tile variants based on format.
    /// </summary>
    public int ExpectedVariantCount => AutoTileFormat == AutoTileFormat.Corner16 ? 16 : 47;

    public bool IsPassable => Passability == TilePassability.Passable;
    public bool HasAutoTileVariants => AutoTileVariants != null;

    public bool IsAllowedInBiome(BiomeType biome) => AllowedBiomes == null || AllowedBiomes.Contains(biome);

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

        if (AutoTileFormat == AutoTileFormat.Blob47)
        {
            // For blob format, convert 8-bit mask to 0-46 index
            index = NeighborBitmask8.GetBlobIndex(bitmask);
            maxIndex = 47;
        }
        else
        {
            // For corner format, use mask directly as index
            index = bitmask;
            maxIndex = 16;
        }

        if (index < 0 || index >= maxIndex || index >= AutoTileVariants.Length)
            return AtlasCoords;

        return AutoTileVariants[index] ?? AtlasCoords;
    }
}
