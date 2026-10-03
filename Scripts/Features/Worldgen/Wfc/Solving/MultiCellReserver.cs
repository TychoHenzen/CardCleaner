using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Reserves the extra cells occupied by multi-cell auto-tile variants after a cell collapse.
/// Grid-specific because it needs positional offsets.
/// </summary>
internal sealed class MultiCellReserver
{
    private readonly ITileRegistry? _tileRegistry;

    internal MultiCellReserver(ITileRegistry? tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    internal void Reserve(Vector2I anchorPos, string tileId, WfcGrid grid)
    {
        var bounds = FindAutoTileFormat(tileId)?.GetMaxMultiCellBounds();
        if (bounds == null)
            return;

        ReserveBlock(anchorPos, bounds.Value.Size, bounds.Value.Offset, grid);
    }

    private AutoTileFormatDefinition? FindAutoTileFormat(string tileId)
    {
        var tileDef = _tileRegistry?.GetTile(tileId);
        if (tileDef == null || !tileDef.HasAutoTileVariants)
            return null;

        return tileDef.GetAutoTileFormat();
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
