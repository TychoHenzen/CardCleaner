#if TOOLS
namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Mutable variant configuration for a format definition.
/// Contains default Size/Offset for variants of this format.
/// </summary>
public class EditableFormatVariant
{
    /// <summary>Size in cells (default 1x1).</summary>
    public int SizeX { get; set; } = 1;
    public int SizeY { get; set; } = 1;

    /// <summary>Offset from anchor cell (default 0,0).</summary>
    public int OffsetX { get; set; } = 0;
    public int OffsetY { get; set; } = 0;

    public EditableFormatVariant Clone()
    {
        return new EditableFormatVariant
        {
            SizeX = SizeX,
            SizeY = SizeY,
            OffsetX = OffsetX,
            OffsetY = OffsetY
        };
    }
}
#endif
