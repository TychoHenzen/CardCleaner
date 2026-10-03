#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Mutable variant definition for editing auto-tile variants.
/// Corresponds to the immutable VariantDefinition record in the runtime.
/// </summary>
public class EditableVariantDefinition
{
    /// <summary>Atlas X coordinate for this variant.</summary>
    public int AtlasX { get; set; }

    /// <summary>Atlas Y coordinate for this variant.</summary>
    public int AtlasY { get; set; }

    /// <summary>Width in cells (default 1).</summary>
    public int SizeX { get; set; } = 1;

    /// <summary>Height in cells (default 1).</summary>
    public int SizeY { get; set; } = 1;

    /// <summary>X anchor offset from logical cell position.</summary>
    public int OffsetX { get; set; }

    /// <summary>Y anchor offset from logical cell position (negative = above).</summary>
    public int OffsetY { get; set; }

    /// <summary>Override atlas region width (null = use tileset default).</summary>
    public int? AtlasRegionWidth { get; set; }

    /// <summary>Override atlas region height (null = use tileset default).</summary>
    public int? AtlasRegionHeight { get; set; }

    /// <summary>Returns true if this is a multi-cell variant.</summary>
    public bool IsMultiCell => SizeX > 1 || SizeY > 1;

    /// <summary>Returns true if this variant has a non-zero offset.</summary>
    public bool HasOffset => OffsetX != 0 || OffsetY != 0;

    /// <summary>Gets the atlas coordinates as a Vector2I.</summary>
    public Vector2I AtlasCoords => new(AtlasX, AtlasY);

    /// <summary>Gets the size as a Vector2I.</summary>
    public Vector2I Size => new(SizeX, SizeY);

    /// <summary>Gets the offset as a Vector2I.</summary>
    public Vector2I Offset => new(OffsetX, OffsetY);

    /// <summary>
    /// Converts to the immutable runtime VariantDefinition.
    /// </summary>
    public Features.Worldgen.AutoTiling.VariantDefinition ToVariantDefinition()
    {
        Vector2I? atlasRegionSize = (AtlasRegionWidth.HasValue || AtlasRegionHeight.HasValue)
            ? new Vector2I(AtlasRegionWidth ?? 0, AtlasRegionHeight ?? 0)
            : null;

        return new Features.Worldgen.AutoTiling.VariantDefinition(
            AtlasCoords,
            Size,
            Offset,
            atlasRegionSize
        );
    }

    /// <summary>
    /// Creates from an immutable runtime VariantDefinition.
    /// </summary>
    public static EditableVariantDefinition FromVariantDefinition(
        Features.Worldgen.AutoTiling.VariantDefinition def)
    {
        return new EditableVariantDefinition
        {
            AtlasX = def.AtlasCoords.X,
            AtlasY = def.AtlasCoords.Y,
            SizeX = def.Size.X,
            SizeY = def.Size.Y,
            OffsetX = def.Offset.X,
            OffsetY = def.Offset.Y,
            AtlasRegionWidth = def.AtlasRegionSize?.X,
            AtlasRegionHeight = def.AtlasRegionSize?.Y
        };
    }

    /// <summary>
    /// Creates a deep copy of this variant definition.
    /// </summary>
    public EditableVariantDefinition Clone()
    {
        return new EditableVariantDefinition
        {
            AtlasX = AtlasX,
            AtlasY = AtlasY,
            SizeX = SizeX,
            SizeY = SizeY,
            OffsetX = OffsetX,
            OffsetY = OffsetY,
            AtlasRegionWidth = AtlasRegionWidth,
            AtlasRegionHeight = AtlasRegionHeight
        };
    }
}
#endif
