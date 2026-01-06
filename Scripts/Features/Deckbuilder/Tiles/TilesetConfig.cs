using Godot;

namespace CardCleaner.Features.Deckbuilder.Tiles;

/// <summary>
/// Configuration for tileset-level spatial properties that affect all tiles.
/// </summary>
public sealed class TilesetConfig
{
    /// <summary>
    /// Default configuration with 16x16 base tile size and no grid offset.
    /// </summary>
    public static readonly TilesetConfig Default = new();

    /// <summary>
    /// Base tile size in pixels (width, height). Default is 16x16.
    /// Common values: 16x16, 24x24, 32x32.
    /// </summary>
    public Vector2I BaseTileSize { get; init; } = new(16, 16);

    /// <summary>
    /// Grid offset for half-tile shifted tilesets. Default is (0, 0).
    /// A value of (0.5, 0.5) would shift the grid by half a tile in both dimensions.
    /// </summary>
    public Vector2 GridOffset { get; init; } = Vector2.Zero;

    /// <summary>
    /// Returns true if this config differs from the default.
    /// </summary>
    public bool IsNonDefault =>
        BaseTileSize != new Vector2I(16, 16) || GridOffset != Vector2.Zero;
}
