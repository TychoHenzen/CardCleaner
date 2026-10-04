using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Abstracts the topology (cell arrangement and neighbor relationships) for WFC.
/// Allows WFC to work on different grid types (rectangular, irregular mesh, etc.).
///
/// <see cref="GetNeighbors"/> is adjacency, where adjacency rules apply. <see cref="GetWindowNeighbors"/>
/// adds the diagonal cells of a grid's 2x2 windows; on a mesh the two are the same.
/// </summary>
public interface IWfcTopology
{
    /// <summary>
    /// Total number of cells in the topology.
    /// </summary>
    int CellCount { get; }

    /// <summary>
    /// Gets the WFC state for a cell.
    /// </summary>
    /// <param name="cellId">Cell identifier (0 to CellCount-1).</param>
    WfcCellState GetCell(int cellId);

    /// <summary>
    /// Checks if a cell ID is valid.
    /// </summary>
    bool IsValidCell(int cellId);

    /// <summary>
    /// Enumerates all cell IDs.
    /// </summary>
    IEnumerable<int> GetAllCellIds();

    /// <summary>Gets cells adjacent to this cell, where adjacency rules apply.</summary>
    /// <param name="cellId">Cell to get neighbors for.</param>
    IEnumerable<int> GetNeighbors(int cellId);

    /// <summary>Writes neighbors into a pre-allocated span and returns the count.</summary>
    /// <param name="cellId">Cell to get neighbors for.</param>
    /// <param name="output">Span to write neighbor IDs into.</param>
    /// <returns>Number of neighbors written.</returns>
    int GetNeighborsNonAlloc(int cellId, Span<int> output);

    /// <summary>Gets cells sharing a visual window with this cell (adjacent cells plus diagonals on grids).</summary>
    /// <param name="cellId">Cell to get window neighbors for.</param>
    IEnumerable<int> GetWindowNeighbors(int cellId) => GetNeighbors(cellId);

    /// <summary>Writes window neighbors into a pre-allocated span and returns the count.</summary>
    /// <param name="cellId">Cell to get window neighbors for.</param>
    /// <param name="output">Span to write neighbor IDs into.</param>
    /// <returns>Number of neighbors written.</returns>
    int GetWindowNeighborsNonAlloc(int cellId, Span<int> output) => GetNeighborsNonAlloc(cellId, output);

    /// <summary>Gets the maximum neighbor count of either neighborhood, for buffer allocation.</summary>
    int MaxNeighborCount { get; }

    /// <summary>
    /// Checks if a cell has at least one collapsed neighbor.
    /// Used for frontier-based cell selection.
    /// </summary>
    bool HasCollapsedNeighbor(int cellId);

    /// <summary>
    /// Gets the collapsed tile ID at a cell, or null if not collapsed.
    /// </summary>
    string? GetCollapsedTileAt(int cellId);

    /// <summary>
    /// Checks if all cells have collapsed to a single tile.
    /// </summary>
    bool IsFullyCollapsed();

    /// <summary>
    /// Checks if any cell is in contradiction state (no valid options).
    /// </summary>
    bool HasContradiction();
}
