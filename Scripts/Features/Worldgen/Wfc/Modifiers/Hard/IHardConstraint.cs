using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Hard;

/// <summary>
/// Context provided to hard constraints for validity checking.
/// </summary>
public readonly struct HardConstraintContext
{
    /// <summary>
    /// The grid position being evaluated.
    /// </summary>
    public Vector2I Position { get; init; }

    /// <summary>
    /// The tile ID being evaluated for validity.
    /// </summary>
    public string TileId { get; init; }

    /// <summary>
    /// The current WFC grid state.
    /// </summary>
    public WfcGrid Grid { get; init; }
}

/// <summary>
/// Hard constraints eliminate tile options entirely.
/// They enforce absolute rules that cannot be violated.
/// Examples: adjacency rules, transition spacing, blocking constraints.
/// </summary>
public interface IHardConstraint
{
    /// <summary>
    /// Checks if a tile is valid at the given position.
    /// </summary>
    /// <param name="context">Position, tile, and grid state</param>
    /// <returns>True if tile is allowed, false to eliminate it</returns>
    bool IsValid(HardConstraintContext context);
}
