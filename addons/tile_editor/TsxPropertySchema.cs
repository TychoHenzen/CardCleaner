#if TOOLS
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Defines the required custom properties for TSX tilesets.
/// These properties are used by TiledTilesetLoader to create TileDefinitions.
/// </summary>
public static class TsxPropertySchema
{
    /// <summary>
    /// Properties required for individual tiles in the TSX tileset.
    /// These are applied to tile elements based on their position in the tileset.
    /// </summary>
    public static readonly List<TsxProperty> TileProperties = new()
    {
        // Core identification
        TsxProperty.String("id", ""),           // Tile ID (snake_case), typically set per-tile
        TsxProperty.String("name", ""),         // Display name, typically set per-tile

        // Gameplay properties
        TsxProperty.String("passability", "passable"),  // passable|solid|partially_passable
        TsxProperty.String("layer", "terrain"),         // terrain|decoration|structure|effects
        TsxProperty.Float("elevation", 0f),             // Height for 3D effects
        TsxProperty.Bool("istransparent", true),        // Whether tile has transparency

        // Rendering
        TsxProperty.Int("dominance", 0),                // Visual priority for terrain transitions (higher = on top)

        // Decoration-specific
        TsxProperty.Float("decorationdensity", 1f),     // 0.0-1.0 probability for decoration placement

        // Auto-tile terrain references (optional, for auto-tiles only)
        // TsxProperty.String("innerterrain", ""),      // ID of inner terrain (what the border shows)
        // TsxProperty.String("outerterrain", ""),      // ID of outer terrain ("*" = compositable)
    };

    /// <summary>
    /// Properties required for wang sets (auto-tile configurations).
    /// These are applied to wangset elements.
    /// </summary>
    public static readonly List<TsxProperty> WangSetProperties = new()
    {
        // Whether the auto-tile has transparent background (border-only)
        // If true, the border is composited onto base terrains at atlas compile time
        TsxProperty.Bool("TransparentBackground", false),

        // Terrain references for auto-tile transitions
        TsxProperty.String("OuterTerrain", ""),   // "*" = compositable, or specific tile ID
        TsxProperty.String("InnerTerrain", ""),   // Tile ID that fills the interior
    };

    /// <summary>
    /// Common biome affinity properties. These are optional and define
    /// how strongly a tile is associated with each biome (0.0-1.0).
    /// Only non-zero values need to be set.
    /// </summary>
    public static readonly List<TsxProperty> BiomeAffinityProperties = new()
    {
        TsxProperty.Float("biome_grassland", 0f),
        TsxProperty.Float("biome_forest", 0f),
        TsxProperty.Float("biome_desert", 0f),
        TsxProperty.Float("biome_tundra", 0f),
        TsxProperty.Float("biome_swamp", 0f),
        TsxProperty.Float("biome_mountain", 0f),
        TsxProperty.Float("biome_water", 0f),
        TsxProperty.Float("biome_volcanic", 0f),
    };

    /// <summary>
    /// Gets the default value for a tile property by name.
    /// Returns null if the property is not in the schema.
    /// </summary>
    public static TsxProperty? GetTilePropertyDefault(string name)
    {
        foreach (var prop in TileProperties)
        {
            if (string.Equals(prop.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return prop;
        }

        foreach (var prop in BiomeAffinityProperties)
        {
            if (string.Equals(prop.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return prop;
        }

        return null;
    }

    /// <summary>
    /// Gets the default value for a wang set property by name.
    /// Returns null if the property is not in the schema.
    /// </summary>
    public static TsxProperty? GetWangSetPropertyDefault(string name)
    {
        foreach (var prop in WangSetProperties)
        {
            if (string.Equals(prop.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return prop;
        }

        return null;
    }

    /// <summary>
    /// Creates a complete set of tile properties with defaults for a new tile.
    /// Only includes core properties, not biome affinities (those should be set explicitly).
    /// </summary>
    /// <param name="id">The tile ID to set</param>
    /// <param name="name">The tile display name</param>
    public static List<TsxProperty> CreateDefaultTileProperties(string id, string name)
    {
        return new List<TsxProperty>
        {
            TsxProperty.String("id", id),
            TsxProperty.String("name", name),
            TsxProperty.String("passability", "passable"),
            TsxProperty.String("layer", "terrain"),
            TsxProperty.Float("elevation", 0f),
            TsxProperty.Bool("istransparent", true),
            TsxProperty.Int("dominance", 0),
            TsxProperty.Float("decorationdensity", 1f),
        };
    }

    /// <summary>
    /// Creates a complete set of wang set properties with defaults.
    /// </summary>
    /// <param name="isTransparent">Whether the auto-tile has transparent borders</param>
    public static List<TsxProperty> CreateDefaultWangSetProperties(bool isTransparent = false)
    {
        return new List<TsxProperty>
        {
            TsxProperty.Bool("TransparentBackground", isTransparent),
            TsxProperty.String("OuterTerrain", isTransparent ? "*" : ""),
            TsxProperty.String("InnerTerrain", ""),
        };
    }

    /// <summary>
    /// Validates a passability value.
    /// </summary>
    public static bool IsValidPassability(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "passable" or "solid" or "partially_passable" => true,
            _ => false
        };
    }

    /// <summary>
    /// Validates a layer value.
    /// </summary>
    public static bool IsValidLayer(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "terrain" or "decoration" or "structure" or "effects" => true,
            _ => false
        };
    }
}
#endif
