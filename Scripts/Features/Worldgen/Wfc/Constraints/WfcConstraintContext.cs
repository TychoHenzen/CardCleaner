using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Precomputed neighbor state for constraint evaluation.
/// Computed once per position to eliminate redundant neighbor iteration.
/// </summary>
public readonly struct CollapsedNeighborInfo
{
    /// <summary>
    /// Collapsed tiles in 4-way neighbors (N, E, S, W).
    /// Key is neighbor position, value is tile ID.
    /// </summary>
    public IReadOnlyDictionary<Vector2I, string> Neighbors4 { get; init; }

    /// <summary>
    /// Collapsed tiles in 8-way neighbors (includes diagonals).
    /// Key is neighbor position, value is tile ID.
    /// </summary>
    public IReadOnlyDictionary<Vector2I, string> Neighbors8 { get; init; }

    /// <summary>
    /// Count of 4-way neighbors that match the candidate tile.
    /// </summary>
    public int SameType4Count { get; init; }

    /// <summary>
    /// Count of 8-way neighbors that match the candidate tile.
    /// </summary>
    public int SameType8Count { get; init; }

    /// <summary>
    /// Whether any 4-way neighbor is collapsed.
    /// </summary>
    public bool HasCollapsedNeighbor4 { get; init; }

    /// <summary>
    /// Whether any 8-way neighbor is collapsed.
    /// </summary>
    public bool HasCollapsedNeighbor8 { get; init; }
}

/// <summary>
/// Context provided to WFC constraints for probability calculation.
/// Unifies the context structures from IHardConstraint and ISoftModifier.
/// Includes precomputed neighbor state to eliminate redundant iteration.
/// </summary>
public readonly struct WfcConstraintContext
{
    /// <summary>
    /// The grid position being evaluated.
    /// </summary>
    public Vector2I Position { get; init; }

    /// <summary>
    /// The tile ID being evaluated for placement.
    /// </summary>
    public string TileId { get; init; }

    /// <summary>
    /// The current WFC grid state.
    /// </summary>
    public WfcGrid Grid { get; init; }

    /// <summary>
    /// Random number generator for stochastic constraints.
    /// May be null for deterministic constraints.
    /// </summary>
    public RandomNumberGenerator? Rng { get; init; }

    /// <summary>
    /// Precomputed collapsed neighbor information.
    /// Use this instead of iterating Grid.GetNeighbors* directly.
    /// </summary>
    public CollapsedNeighborInfo? NeighborInfo { get; init; }

    /// <summary>
    /// Creates context with precomputed neighbor info for a specific tile candidate.
    /// </summary>
    public static WfcConstraintContext CreateWithNeighborInfo(
        Vector2I position,
        string tileId,
        WfcGrid grid,
        RandomNumberGenerator? rng,
        IReadOnlyDictionary<Vector2I, string> neighbors4,
        IReadOnlyDictionary<Vector2I, string> neighbors8)
    {
        var sameType4 = 0;
        var sameType8 = 0;

        foreach (var kvp in neighbors4)
        {
            if (kvp.Value == tileId)
                sameType4++;
        }

        foreach (var kvp in neighbors8)
        {
            if (kvp.Value == tileId)
                sameType8++;
        }

        return new WfcConstraintContext
        {
            Position = position,
            TileId = tileId,
            Grid = grid,
            Rng = rng,
            NeighborInfo = new CollapsedNeighborInfo
            {
                Neighbors4 = neighbors4,
                Neighbors8 = neighbors8,
                SameType4Count = sameType4,
                SameType8Count = sameType8,
                HasCollapsedNeighbor4 = neighbors4.Count > 0,
                HasCollapsedNeighbor8 = neighbors8.Count > 0
            }
        };
    }
}
