using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Reserves the extra cells occupied by multi-cell auto-tile variants after a cell collapse.
/// Grid-specific because it needs positional offsets.
/// </summary>
internal sealed class MultiCellReserver
{
    private readonly IWfcTileCatalog? _tileCatalog;

    internal MultiCellReserver(IWfcTileCatalog? tileCatalog)
    {
        _tileCatalog = tileCatalog;
    }

    internal void Reserve(Vector2I anchorPos, string tileId, WfcGrid grid)
    {
        if (_tileCatalog == null || !_tileCatalog.IsAutoTile(tileId))
            return;

        var bounds = _tileCatalog.GetMultiCellBounds(tileId);
        if (bounds == null)
            return;

        ReserveBlock(anchorPos, bounds.Value.Size, bounds.Value.Offset, grid);
    }

    private static void ReserveBlock(Vector2I anchorPos, Vector2I size, Vector2I offset, WfcGrid grid)
    {
        for (var dy = 0; dy < size.Y; dy++)
        {
            for (var dx = 0; dx < size.X; dx++)
            {
                if (dx == 0 && dy == 0 && offset == Vector2I.Zero)
                    continue;

                var reservedPos = anchorPos + offset + new Vector2I(dx, dy);
                if (!grid.IsInBounds(reservedPos) || reservedPos == anchorPos)
                    continue;

                var cell = grid.GetCell(reservedPos);
                if (!cell.IsCollapsed() && !cell.IsReserved)
                    cell.Reserve(anchorPos);
            }
        }
    }
}
