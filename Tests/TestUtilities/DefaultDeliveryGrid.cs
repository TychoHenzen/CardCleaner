using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
///     The slot grid of a <see cref="DeliveryMarker" /> that sets no grid of its own, for tests that compute where a
///     delivery lands without holding the marker.
/// </summary>
public static class DefaultDeliveryGrid
{
    public const int Columns = DeliveryMarker.DefaultColumns;
    public const int Rows = DeliveryMarker.DefaultRows;
    public const int SlotCount = Columns * Rows;
    public const float CellWidth = DeliveryMarker.DefaultCellWidth;
    public const float CellDepth = DeliveryMarker.DefaultCellDepth;

    public static Vector2 CellSize => new(CellWidth, CellDepth);

    /// <summary>Offset of slot <paramref name="index" />'s centre from the marker.</summary>
    public static Vector3 SlotOffset(int index) => DeliveryMarker.GridOffset(index, Columns, Rows, CellSize);
}
