namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Unified WFC constraint interface that works with any IWfcGrid implementation.
/// This replaces both IWfcConstraint (grid-based) and IMeshWfcConstraint (mesh-based).
/// </summary>
public interface IUnifiedWfcConstraint
{
    /// <summary>
    /// Returns a weight modifier for placing a tile at the specified cell.
    /// The modifier is multiplied with the tile's base weight during selection.
    /// </summary>
    /// <param name="cellId">The cell where the tile would be placed.</param>
    /// <param name="tileId">The tile being evaluated.</param>
    /// <param name="grid">The current WFC grid state.</param>
    /// <returns>
    /// Weight modifier:
    /// - 0.0 = forbidden (hard constraint)
    /// - Less than 1.0 = penalty (soft discourage)
    /// - 1.0 = neutral
    /// - Greater than 1.0 = boost (soft encourage)
    /// </returns>
    float GetWeightModifier(int cellId, string tileId, IWfcGrid grid);

    /// <summary>
    /// Called when a tile is collapsed at a cell.
    /// Allows constraints to update internal state (e.g., region tracking).
    /// </summary>
    /// <param name="cellId">The cell that was collapsed.</param>
    /// <param name="tileId">The tile that was selected.</param>
    /// <param name="grid">The current WFC grid state.</param>
    void OnTileCollapsed(int cellId, string tileId, IWfcGrid grid);

    /// <summary>
    /// Resets the constraint state for a new generation.
    /// Called before starting a solve operation.
    /// </summary>
    void Reset();
}
