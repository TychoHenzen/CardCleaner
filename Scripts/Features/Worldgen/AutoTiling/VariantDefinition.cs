using Godot;

namespace CardCleaner.Features.Worldgen.AutoTiling;

/// <summary>
/// Immutable definition of a single auto-tile variant, specifying its atlas location
/// and optional spatial properties for multi-cell variants.
/// </summary>
/// <param name="AtlasCoords">Position of this variant in the atlas texture (in tile coordinates).</param>
/// <param name="Size">
/// Number of cells this variant occupies (width, height). Default is (1, 1).
/// Multi-cell variants like 3-tile-tall walls use values like (1, 3).
/// </param>
/// <param name="Offset">
/// Anchor point offset from the logical cell position. Default is (0, 0).
/// For tall variants rendered above the logical cell, use negative Y offset (e.g., (0, -2)).
/// </param>
/// <param name="AtlasRegionSize">
/// Override for the source region size in the atlas if non-standard.
/// Null means use the tileset's default tile size. Useful for tilesets with
/// varying tile sizes.
/// </param>
public readonly record struct VariantDefinition(
    Vector2I AtlasCoords,
    Vector2I Size = default,
    Vector2I Offset = default,
    Vector2I? AtlasRegionSize = null)
{
    /// <summary>
    /// Size of the variant in cells, with default (1, 1) if not specified.
    /// </summary>
    public Vector2I Size { get; init; } = Size == default ? Vector2I.One : Size;

    /// <summary>
    /// Returns true if this variant spans multiple cells.
    /// </summary>
    public bool IsMultiCell => Size.X > 1 || Size.Y > 1;

    /// <summary>
    /// Returns true if this variant has a non-zero anchor offset.
    /// </summary>
    public bool HasOffset => Offset != Vector2I.Zero;
}
