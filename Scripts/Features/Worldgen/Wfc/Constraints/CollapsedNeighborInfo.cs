using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Precomputed neighbor state for constraint evaluation.
/// Computed once per cell to eliminate redundant neighbor iteration.
/// Uses cell IDs (int) to be topology-agnostic.
/// </summary>
public readonly struct CollapsedNeighborInfo
{
    /// <summary>
    /// Collapsed tiles in adjacent cells (topology-defined adjacency; edge-sharing on grids).
    /// Key is neighbor cell ID, value is tile ID.
    /// </summary>
    public IReadOnlyDictionary<int, string> Neighbors { get; init; }

    /// <summary>
    /// Collapsed tiles in cells sharing a visual window with the cell (adds diagonals on grids).
    /// Key is neighbor cell ID, value is tile ID.
    /// </summary>
    public IReadOnlyDictionary<int, string> WindowNeighbors { get; init; }

    /// <summary>
    /// Count of neighbors that match the candidate tile type.
    /// </summary>
    public int SameTypeCount { get; init; }

    /// <summary>
    /// Whether any neighbor is collapsed.
    /// </summary>
    public bool HasCollapsedNeighbor { get; init; }
}
