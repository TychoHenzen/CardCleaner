using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Context provided to soft modifiers for weight calculation.
/// Contains position, grid state, and tile being evaluated.
/// </summary>
public readonly struct SoftModifierContext
{
    /// <summary>
    /// The grid position where a tile is being selected.
    /// </summary>
    public Vector2I Position { get; init; }

    /// <summary>
    /// The tile ID being evaluated for selection.
    /// </summary>
    public string TileId { get; init; }

    /// <summary>
    /// The current WFC grid state.
    /// </summary>
    public WfcGrid Grid { get; init; }
}

/// <summary>
/// Soft modifiers adjust tile selection weights without eliminating options.
/// They influence probability but don't enforce hard constraints.
/// Examples: continuity bias, diminishing returns, noise variation.
/// </summary>
public interface ISoftModifier
{
    /// <summary>
    /// Calculates a weight multiplier for the given tile in context.
    /// </summary>
    /// <param name="context">Position, tile, and grid state</param>
    /// <returns>Multiplier to apply to base weight (1.0 = no change)</returns>
    float CalculateMultiplier(SoftModifierContext context);
}
