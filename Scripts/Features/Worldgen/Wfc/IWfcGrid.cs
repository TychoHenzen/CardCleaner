using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Unified interface for WFC grids that can be either regular 2D grids or irregular mesh grids.
/// Cell IDs are integers - for 2D grids use linearized index (y * width + x).
/// </summary>
public interface IWfcGrid
{
    /// <summary>
    /// Total number of cells in the grid.
    /// </summary>
    int CellCount { get; }

    /// <summary>
    /// Gets the WFC cell state for the given cell ID.
    /// </summary>
    WfcCellState GetCell(int cellId);

    /// <summary>
    /// Checks if the cell ID is valid for this grid.
    /// </summary>
    bool IsValidCell(int cellId);

    /// <summary>
    /// Gets the IDs of neighboring cells (4-connected for grid, edge-connected for mesh).
    /// </summary>
    IEnumerable<int> GetNeighbors(int cellId);

    /// <summary>
    /// Gets all cell IDs in the grid.
    /// </summary>
    IEnumerable<int> GetAllCellIds();

    /// <summary>
    /// Checks if all cells are collapsed.
    /// </summary>
    bool IsFullyCollapsed();

    /// <summary>
    /// Checks if any cell has no valid options (contradiction).
    /// </summary>
    bool HasContradiction();

    /// <summary>
    /// Checks if the cell has at least one collapsed neighbor.
    /// </summary>
    bool HasCollapsedNeighbor(int cellId);

    /// <summary>
    /// Gets the tile ID of a collapsed cell, or null if not collapsed.
    /// </summary>
    string? GetCollapsedTileAt(int cellId);

    /// <summary>
    /// Creates a deep copy of the grid state.
    /// </summary>
    IWfcGrid Clone();
}

/// <summary>
/// Extended interface for grids that support 8-way adjacency (orthogonal + diagonal).
/// Regular 2D grids implement this; irregular meshes typically don't.
/// </summary>
public interface IWfcGrid8Way : IWfcGrid
{
    /// <summary>
    /// Gets the IDs of 8-connected neighbors (orthogonal + diagonal).
    /// </summary>
    IEnumerable<int> GetNeighbors8(int cellId);
}

/// <summary>
/// Interface for grids that have a 2D coordinate system.
/// Regular grids implement this for backward compatibility with position-based constraints.
/// </summary>
public interface IWfcGridWithCoordinates : IWfcGrid
{
    /// <summary>
    /// Grid width in cells.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Grid height in cells.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Converts a cell ID to its 2D position.
    /// </summary>
    Godot.Vector2I CellIdToPosition(int cellId);

    /// <summary>
    /// Converts a 2D position to its cell ID.
    /// </summary>
    int PositionToCellId(Godot.Vector2I position);
}
