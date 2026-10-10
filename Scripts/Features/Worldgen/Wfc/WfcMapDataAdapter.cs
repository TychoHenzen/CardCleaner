using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Copies a collapsed WfcGrid into the tile grid that WfcGenerationResult carries.
/// </summary>
public static class WfcMapDataAdapter
{
    /// <summary>
    /// Converts a fully collapsed WFC grid to a tile id grid indexed [y, x].
    /// </summary>
    /// <param name="grid">Fully collapsed WFC grid</param>
    /// <param name="defaultTileId">Optional fallback tile ID; null uses the first available.</param>
    /// <returns>Tile ids, one per cell</returns>
    public static string[,] ToTileIds(WfcGrid grid, string? defaultTileId = null)
    {
        var tileIds = new string[grid.Height, grid.Width];
        FillTileIds(grid, tileIds, defaultTileId);
        return tileIds;
    }

    /// <summary>
    /// Copies each cell tile into tileIds.
    /// An uncollapsed cell (not expected in a well-formed grid) uses its first possible tile.
    /// </summary>
    private static void FillTileIds(WfcGrid grid, string[,] tileIds, string? defaultTileId)
    {
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var pos = new Vector2I(x, y);
                var cell = grid.GetCell(pos);

                tileIds[y, x] = cell.IsCollapsed()
                    ? cell.GetCollapsedTile()
                    : GetFirstTile(cell) ?? defaultTileId ?? string.Empty;
            }
        }
    }

    private static string? GetFirstTile(WfcCellState cell)
    {
        foreach (var tile in cell.GetPossibleTiles())
            return tile;
        return null;
    }
}
