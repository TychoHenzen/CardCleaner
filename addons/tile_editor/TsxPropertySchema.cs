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
    ///
    /// NOTE: The tile ID is set via the 'class' attribute on the tile element,
    /// e.g., &lt;tile id="507" class="dirt"/&gt;. The 'class' attribute serves as the tile ID.
    /// (Wang sets use the 'type' attribute instead.)
    ///
    /// Custom enum types are defined in Data/Tiled/Tiles.tiled-project:
    /// - Passability: passable, solid, partially_passable
    /// - Layer: terrain, decoration, structure, effects
    /// - Biome: flags enum (plains, forest, desert, tundra, swamp, mountains, water, cave, volcanic, magical)
    /// </summary>
    public static readonly List<TsxProperty> TileProperties = new()
    {
        // Display name - shown in UI (the 'class' attribute is the tile ID)
        TsxProperty.String("name", ""),

        // Gameplay properties (use Tiled custom enum types from Tiles.tiled-project)
        TsxProperty.StringEnum("passability", "Passability", "passable"),
        TsxProperty.StringEnum("layer", "Layer", "terrain"),
        TsxProperty.IntEnum("biome", "Biome", 0),  // Flags enum (bitfield, 0 = no biomes)

        TsxProperty.Float("elevation", 0f),
        TsxProperty.Bool("istransparent", true),
    };

    /// <summary>
    /// Properties required for wang sets (auto-tile configurations).
    /// These are applied to wangset elements.
    /// Note: The tile ID is set via the 'type' attribute on the wangset element.
    /// </summary>
    public static readonly List<TsxProperty> WangSetProperties = new()
    {
        // Gameplay properties (use Tiled custom enum types from Tiles.tiled-project)
        TsxProperty.StringEnum("passability", "Passability", "passable"),
        TsxProperty.StringEnum("layer", "Layer", "terrain"),
        TsxProperty.IntEnum("biome", "Biome", 0),  // Flags enum (bitfield, 0 = no biomes)
        TsxProperty.Float("elevation", 0f),

        // Whether the auto-tile has transparent background (border-only)
        // If true, the border is composited onto base terrains at atlas compile time
        TsxProperty.Bool("TransparentBackground", false),

        // Terrain references for auto-tile transitions
        // "$self" = use this tile's ID as inner terrain (default)
        // "*" = compositable with any outer terrain (default)
        TsxProperty.String("InnerTerrain", "$self"),
        TsxProperty.String("OuterTerrain", "*"),
    };

    /// <summary>
    /// Biome flag values for the Biome flags enum.
    /// These match the order in Data/Tiled/Tiles.tiled-project.
    /// Use bitwise OR to combine multiple biomes.
    /// </summary>
    public static class BiomeFlags
    {
        public const int None = 0;
        public const int Plains = 1 << 0;      // 1
        public const int Forest = 1 << 1;      // 2
        public const int Desert = 1 << 2;      // 4
        public const int Tundra = 1 << 3;      // 8
        public const int Swamp = 1 << 4;       // 16
        public const int Mountains = 1 << 5;   // 32
        public const int Water = 1 << 6;       // 64
        public const int Cave = 1 << 7;        // 128
        public const int Volcanic = 1 << 8;    // 256
        public const int Magical = 1 << 9;     // 512
    }

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
    ///
    /// NOTE: The tile ID is set via the 'class' attribute on the tile element, not as a property.
    /// </summary>
    /// <param name="name">The tile display name</param>
    /// <param name="biomeFlags">Biome flags (use BiomeFlags constants, combine with |)</param>
    public static List<TsxProperty> CreateDefaultTileProperties(string name, int biomeFlags = 0)
    {
        return new List<TsxProperty>
        {
            TsxProperty.String("name", name),
            TsxProperty.StringEnum("passability", "Passability", "passable"),
            TsxProperty.StringEnum("layer", "Layer", "terrain"),
            TsxProperty.IntEnum("biome", "Biome", biomeFlags),
            TsxProperty.Float("elevation", 0f),
            TsxProperty.Bool("istransparent", true),
        };
    }

    /// <summary>
    /// Creates a complete set of wang set properties with defaults.
    /// </summary>
    /// <param name="isTransparent">Whether the auto-tile has transparent borders</param>
    /// <param name="biomeFlags">Biome flags (use BiomeFlags constants, combine with |)</param>
    public static List<TsxProperty> CreateDefaultWangSetProperties(bool isTransparent = false, int biomeFlags = 0)
    {
        return new List<TsxProperty>
        {
            TsxProperty.StringEnum("passability", "Passability", "passable"),
            TsxProperty.StringEnum("layer", "Layer", "terrain"),
            TsxProperty.IntEnum("biome", "Biome", biomeFlags),
            TsxProperty.Float("elevation", 0f),
            TsxProperty.Bool("TransparentBackground", isTransparent),
            TsxProperty.String("InnerTerrain", "$self"),
            TsxProperty.String("OuterTerrain", "*"),
        };
    }

    /// <summary>
    /// Known biome IDs that match tiles.json and Tiled project definitions.
    /// Order matches BiomeFlags bit positions.
    /// </summary>
    public static readonly string[] KnownBiomeIds = new[]
    {
        "plains", "forest", "desert", "tundra", "swamp",
        "mountains", "water", "cave", "volcanic", "magical"
    };

    /// <summary>
    /// Gets the biome flag for a biome ID string.
    /// </summary>
    public static int GetBiomeFlag(string biomeId) => biomeId.ToLowerInvariant() switch
    {
        "plains" => BiomeFlags.Plains,
        "forest" => BiomeFlags.Forest,
        "desert" => BiomeFlags.Desert,
        "tundra" => BiomeFlags.Tundra,
        "swamp" => BiomeFlags.Swamp,
        "mountains" => BiomeFlags.Mountains,
        "water" => BiomeFlags.Water,
        "cave" => BiomeFlags.Cave,
        "volcanic" => BiomeFlags.Volcanic,
        "magical" => BiomeFlags.Magical,
        _ => BiomeFlags.None
    };

    /// <summary>
    /// Checks if a biome flag is set in the biome flags value.
    /// </summary>
    public static bool HasBiome(int biomeFlags, int biomeFlag) => (biomeFlags & biomeFlag) != 0;
}
#endif
