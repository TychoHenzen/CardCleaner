using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Utility for dual-grid auto-tiling where visual tiles are offset by half a tile
///     from the data grid. Each visual tile sits at the intersection of 4 data cells
///     and computes its bitmask from those 4 corners.
///
///     This approach prevents invalid auto-tile states because visual tiles always
///     sample from the actual data grid state rather than checking diagonal neighbors.
///
///     Data grid: What the user edits (is this cell filled?)
///     Visual grid: Offset by (-0.5, -0.5) tiles, size is (dataWidth+1, dataHeight+1)
///
///     For visual tile at index (vx, vy), the 4 sampled data cells are:
///     - TopLeft (NW):     data[vy-1, vx-1]
///     - TopRight (NE):    data[vy-1, vx]
///     - BottomLeft (SW):  data[vy, vx-1]
///     - BottomRight (SE): data[vy, vx]
///
///     Uses Corner16 bitmask format: NE=1, SE=2, SW=4, NW=8
///
///     ## Auto-Tile Format Selection Guide
///
///     ### Corner16 (Dual-Grid) - Recommended for terrain transitions
///     - 16 tiles, checks 4 diagonal corners
///     - Use with dual-grid technique (visual tiles at half-tile offset)
///     - Each visual tile samples 4 terrain cells at its corners
///     - Prevents invalid states: bitmask always reflects actual terrain
///     - Best for: terrain edges, grass/dirt transitions, water shores
///
///     ### Edge16 (Cardinal) - For wall-like structures
///     - 16 tiles, checks 4 cardinal neighbors (N, E, S, W)
///     - Single-grid technique (visual tiles aligned with data)
///     - Bitmask from same-grid neighbors
///     - Best for: walls, fences, roads, paths
///
///     ### Blob47 (8-bit with constraints) - For detailed tiles
///     - 47 tiles, checks all 8 neighbors with corner constraint
///     - Corners only valid when both adjacent edges are set
///     - Single-grid technique with complex edge handling
///     - Best for: detailed terrain blobs, irregular shapes
/// </summary>
public static class DualGridAutoTile
{
    /// <summary>
    ///     Compute the Corner16 bitmask for a visual tile based on 4 corner data cells.
    /// </summary>
    /// <param name="visualX">X index in the visual grid</param>
    /// <param name="visualY">Y index in the visual grid</param>
    /// <param name="dataGrid">2D boolean array [row, col] where true = filled</param>
    /// <returns>4-bit bitmask (0-15) for Corner16 format</returns>
    public static int ComputeBitmask(int visualX, int visualY, bool[,] dataGrid)
    {
        var dataRows = dataGrid.GetLength(0);
        var dataCols = dataGrid.GetLength(1);

        // Sample the 4 data cells at this visual tile's corners
        var hasNW = IsDataCellFilled(visualX - 1, visualY - 1, dataGrid, dataCols, dataRows);
        var hasNE = IsDataCellFilled(visualX, visualY - 1, dataGrid, dataCols, dataRows);
        var hasSW = IsDataCellFilled(visualX - 1, visualY, dataGrid, dataCols, dataRows);
        var hasSE = IsDataCellFilled(visualX, visualY, dataGrid, dataCols, dataRows);

        // Build bitmask using Corner16 format: NE=1, SE=2, SW=4, NW=8
        var mask = 0;
        if (hasNE) mask |= NeighborBitmaskCorner.NorthEast; // 1
        if (hasSE) mask |= NeighborBitmaskCorner.SouthEast; // 2
        if (hasSW) mask |= NeighborBitmaskCorner.SouthWest; // 4
        if (hasNW) mask |= NeighborBitmaskCorner.NorthWest; // 8

        return mask;
    }

    /// <summary>
    ///     Compute the Corner16 bitmask using a function to check data cells.
    /// </summary>
    /// <param name="visualX">X index in the visual grid</param>
    /// <param name="visualY">Y index in the visual grid</param>
    /// <param name="isDataCellFilled">Function that returns true if data cell at (col, row) is filled</param>
    /// <returns>4-bit bitmask (0-15) for Corner16 format</returns>
    public static int ComputeBitmask(int visualX, int visualY, Func<int, int, bool> isDataCellFilled)
    {
        // Sample the 4 data cells at this visual tile's corners
        var hasNW = isDataCellFilled(visualX - 1, visualY - 1);
        var hasNE = isDataCellFilled(visualX, visualY - 1);
        var hasSW = isDataCellFilled(visualX - 1, visualY);
        var hasSE = isDataCellFilled(visualX, visualY);

        // Build bitmask using Corner16 format: NE=1, SE=2, SW=4, NW=8
        var mask = 0;
        if (hasNE) mask |= NeighborBitmaskCorner.NorthEast; // 1
        if (hasSE) mask |= NeighborBitmaskCorner.SouthEast; // 2
        if (hasSW) mask |= NeighborBitmaskCorner.SouthWest; // 4
        if (hasNW) mask |= NeighborBitmaskCorner.NorthWest; // 8

        return mask;
    }

    /// <summary>
    ///     Compute bitmasks for all visual tiles in a dual-grid setup.
    /// </summary>
    /// <param name="dataGrid">2D boolean array [row, col] where true = filled</param>
    /// <returns>2D array of bitmasks for the visual grid (size: dataRows+1, dataCols+1)</returns>
    public static int[,] ComputeAllBitmasks(bool[,] dataGrid)
    {
        var dataRows = dataGrid.GetLength(0);
        var dataCols = dataGrid.GetLength(1);
        var visualRows = dataRows + 1;
        var visualCols = dataCols + 1;

        var bitmasks = new int[visualRows, visualCols];

        for (var vy = 0; vy < visualRows; vy++)
        for (var vx = 0; vx < visualCols; vx++)
            bitmasks[vy, vx] = ComputeBitmask(vx, vy, dataGrid);

        return bitmasks;
    }

    /// <summary>
    ///     Get the visual grid size for a given data grid size.
    /// </summary>
    public static Vector2I GetVisualGridSize(Vector2I dataGridSize)
    {
        return new Vector2I(dataGridSize.X + 1, dataGridSize.Y + 1);
    }

    /// <summary>
    ///     Get the visual grid size for a given data grid.
    /// </summary>
    public static Vector2I GetVisualGridSize(bool[,] dataGrid)
    {
        return new Vector2I(dataGrid.GetLength(1) + 1, dataGrid.GetLength(0) + 1);
    }

    /// <summary>
    ///     Convert visual grid position to pixel position (with half-tile offset).
    /// </summary>
    /// <param name="visualX">X index in visual grid</param>
    /// <param name="visualY">Y index in visual grid</param>
    /// <param name="tileSize">Size of each tile in pixels</param>
    /// <returns>Pixel position for rendering</returns>
    public static Vector2 GetVisualTilePosition(int visualX, int visualY, Vector2I tileSize)
    {
        // Visual grid is offset by -0.5 tiles
        return new Vector2(
            (visualX - 0.5f) * tileSize.X,
            (visualY - 0.5f) * tileSize.Y
        );
    }

    /// <summary>
    ///     Check if any of the 4 corner data cells are filled (should render visual tile).
    /// </summary>
    public static bool ShouldRenderVisualTile(int visualX, int visualY, bool[,] dataGrid)
    {
        var dataRows = dataGrid.GetLength(0);
        var dataCols = dataGrid.GetLength(1);

        return IsDataCellFilled(visualX - 1, visualY - 1, dataGrid, dataCols, dataRows) ||
               IsDataCellFilled(visualX, visualY - 1, dataGrid, dataCols, dataRows) ||
               IsDataCellFilled(visualX - 1, visualY, dataGrid, dataCols, dataRows) ||
               IsDataCellFilled(visualX, visualY, dataGrid, dataCols, dataRows);
    }

    /// <summary>
    ///     Get count of filled corner data cells for a visual tile (0-4).
    /// </summary>
    public static int GetFilledCornerCount(int visualX, int visualY, bool[,] dataGrid)
    {
        var dataRows = dataGrid.GetLength(0);
        var dataCols = dataGrid.GetLength(1);
        var count = 0;

        if (IsDataCellFilled(visualX - 1, visualY - 1, dataGrid, dataCols, dataRows)) count++;
        if (IsDataCellFilled(visualX, visualY - 1, dataGrid, dataCols, dataRows)) count++;
        if (IsDataCellFilled(visualX - 1, visualY, dataGrid, dataCols, dataRows)) count++;
        if (IsDataCellFilled(visualX, visualY, dataGrid, dataCols, dataRows)) count++;

        return count;
    }

    private static bool IsDataCellFilled(int col, int row, bool[,] dataGrid, int dataCols, int dataRows)
    {
        if (col < 0 || col >= dataCols || row < 0 || row >= dataRows)
            return false;

        return dataGrid[row, col];
    }
}
