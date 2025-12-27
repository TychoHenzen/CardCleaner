using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Configuration for auto-tiling a base tile with 16 edge variants.
///     Uses 4-bit NESW bitmask (N=1, E=2, S=4, W=8) to select variants.
/// </summary>
/// <remarks>
///     The 16 bitmask values represent:
///     0  = No neighbors (isolated)
///     1  = N only
///     2  = E only
///     3  = N+E (NE corner inside)
///     4  = S only
///     5  = N+S (vertical corridor)
///     6  = E+S (SE corner inside)
///     7  = N+E+S (east edge)
///     8  = W only
///     9  = N+W (NW corner inside)
///     10 = E+W (horizontal corridor)
///     11 = N+E+W (north edge)
///     12 = S+W (SW corner inside)
///     13 = N+S+W (west edge)
///     14 = E+S+W (south edge)
///     15 = All neighbors (fully surrounded)
/// </remarks>
[GlobalClass]
public partial class AutoTileConfig : Resource
{
    /// <summary>
    ///     The base tile ID this config applies to.
    ///     Used to identify which tiles should be auto-tiled.
    /// </summary>
    [Export]
    public string BaseTileId { get; set; } = "";

    /// <summary>
    ///     Display name for this auto-tile configuration.
    /// </summary>
    [Export]
    public string DisplayName { get; set; } = "";

    /// <summary>
    ///     The 16 variant tile IDs indexed by bitmask (0-15).
    ///     Null entries fall back to the base tile.
    /// </summary>
    [Export]
    public string?[] Variants { get; set; } = new string?[16];

    /// <summary>
    ///     Get the tile ID for a specific bitmask value.
    /// </summary>
    /// <param name="bitmask">4-bit neighbor bitmask (0-15)</param>
    /// <returns>Variant tile ID, or base tile ID if variant is null</returns>
    public string GetVariant(int bitmask)
    {
        if (bitmask < 0 || bitmask > 15)
            return BaseTileId;

        return Variants[bitmask] ?? BaseTileId;
    }

    /// <summary>
    ///     Check if a tile ID matches this auto-tile config's base tile.
    /// </summary>
    public bool MatchesBaseTile(string tileId) => tileId == BaseTileId;

    /// <summary>
    ///     Check if this config has any variants defined.
    /// </summary>
    public bool HasVariants()
    {
        foreach (var variant in Variants)
            if (!string.IsNullOrEmpty(variant))
                return true;

        return false;
    }

    /// <summary>
    ///     Get the count of defined variants (non-null entries).
    /// </summary>
    public int GetDefinedVariantCount()
    {
        var count = 0;
        foreach (var variant in Variants)
            if (!string.IsNullOrEmpty(variant))
                count++;

        return count;
    }
}
