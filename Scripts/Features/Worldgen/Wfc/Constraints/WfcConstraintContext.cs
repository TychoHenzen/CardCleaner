using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Context provided to WFC constraints for probability calculation.
/// Topology-agnostic: works with rectangular grids, irregular meshes, or any IWfcTopology.
/// </summary>
public readonly struct WfcConstraintContext
{
    /// <summary>
    /// The cell ID being evaluated.
    /// </summary>
    public int CellId { get; init; }

    /// <summary>
    /// The tile ID being evaluated for placement.
    /// </summary>
    public string TileId { get; init; }

    /// <summary>
    /// The current WFC topology state.
    /// </summary>
    public IWfcTopology Topology { get; init; }

    /// <summary>
    /// Random number generator for stochastic constraints.
    /// May be null for deterministic constraints.
    /// </summary>
    public RandomNumberGenerator? Rng { get; init; }

    /// <summary>
    /// Precomputed collapsed neighbor information.
    /// Use this instead of iterating Topology.GetNeighbors directly.
    /// </summary>
    public CollapsedNeighborInfo? NeighborInfo { get; init; }

    // Backward compatibility properties for grid-based constraints

    /// <summary>
    /// Backward compatibility: Grid position (only available when topology is WfcGrid).
    /// </summary>
    public Vector2I Position => Topology is WfcGrid grid
        ? grid.CellIdToPosition(CellId)
        : Vector2I.Zero;

    /// <summary>
    /// Backward compatibility: WfcGrid (only available when topology is WfcGrid).
    /// </summary>
    public WfcGrid Grid => Topology as WfcGrid ?? throw new System.InvalidOperationException(
        "Grid property only available when Topology is WfcGrid");

    /// <summary>
    /// Creates context with precomputed neighbor info for a specific tile candidate.
    /// </summary>
    public static WfcConstraintContext Create(
        int cellId,
        string tileId,
        IWfcTopology topology,
        RandomNumberGenerator? rng,
        IReadOnlyDictionary<int, string> collapsedNeighbors)
    {
        var sameTypeCount = 0;

        foreach (var kvp in collapsedNeighbors)
        {
            if (kvp.Value == tileId)
                sameTypeCount++;
        }

        return new WfcConstraintContext
        {
            CellId = cellId,
            TileId = tileId,
            Topology = topology,
            Rng = rng,
            NeighborInfo = new CollapsedNeighborInfo
            {
                Neighbors = collapsedNeighbors,
                SameTypeCount = sameTypeCount,
                HasCollapsedNeighbor = collapsedNeighbors.Count > 0
            }
        };
    }
}
