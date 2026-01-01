using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Context provided to WFC constraints for probability calculation.
/// Unifies the context structures from IHardConstraint and ISoftModifier.
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
}
