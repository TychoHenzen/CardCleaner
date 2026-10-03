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
    /// Collapsed tiles in all neighbors (topology-defined adjacency).
    /// Key is neighbor cell ID, value is tile ID.
    /// </summary>
    public IReadOnlyDictionary<int, string> Neighbors { get; init; }

    /// <summary>
    /// Count of neighbors that match the candidate tile type.
    /// </summary>
    public int SameTypeCount { get; init; }

    /// <summary>
    /// Whether any neighbor is collapsed.
    /// </summary>
    public bool HasCollapsedNeighbor { get; init; }
}
