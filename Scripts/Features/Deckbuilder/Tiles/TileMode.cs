namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Determines the rendering and behavior mode for a tile definition.
/// Controls which features (variations, auto-tiling, animation) are active.
/// </summary>
public enum TileMode
{
    /// <summary>
    /// A simple tile with fixed atlas coordinates. No variations, auto-tiling, or animation.
    /// </summary>
    PlainTile = 0,

    /// <summary>
    /// Tile with visual variations selected randomly per placement.
    /// Each tile instance can display a different variant.
    /// </summary>
    PerTileVariations = 1,

    /// <summary>
    /// Tile with visual variations where one variant is selected at map generation.
    /// All instances of this tile use the same variant throughout the map.
    /// </summary>
    PerMapVariations = 2,

    /// <summary>
    /// Auto-tiling tile that selects variants based on neighboring tiles.
    /// Uses bitmask-based variant selection (Corner16, Edge16, or Blob47 format).
    /// </summary>
    AutoTile = 3,

    /// <summary>
    /// Combines auto-tiling with per-map variations.
    /// One variation set is selected at generation, then auto-tiling applies within that set.
    /// </summary>
    PerMapVariationAutoTile = 4,

    /// <summary>
    /// Animated tile that cycles through multiple frames at a configurable speed.
    /// </summary>
    AnimatedTile = 5
}
