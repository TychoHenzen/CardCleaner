using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Abstracts the topology (cell arrangement and neighbor relationships) for WFC.
/// Allows WFC to work on different grid types (rectangular, irregular mesh, etc.).
///
/// For rectangular grids: cells are arranged in a 2D array, neighbors include 8-way adjacency.
/// For irregular meshes: cells are vertices, neighbors are all vertices sharing any quad.
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

    /// <summary>Gets cells that share a visual face with this cell.</summary>
    /// <param name="cellId">Cell to get neighbors for.</param>
    IEnumerable<int> GetNeighbors(int cellId);

    /// <summary>Writes neighbors into a pre-allocated span and returns the count.</summary>
    /// <param name="cellId">Cell to get neighbors for.</param>
    /// <param name="output">Span to write neighbor IDs into.</param>
    /// <returns>Number of neighbors written.</returns>
    int GetNeighborsNonAlloc(int cellId, Span<int> output);

    /// <summary>Gets the maximum neighbor count for buffer allocation.</summary>
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
