using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Optional interface for constraints to report which cells need entropy recalculation
/// after a tile is collapsed. Constraints that only depend on immediate neighbors
/// don't need to implement this - the solver handles neighbor invalidation by default.
/// </summary>
public interface IEntropyInvalidator
{
    /// <summary>
    /// Returns additional cells (beyond immediate neighbors) whose entropy is affected
    /// by this collapse. Called after a cell is collapsed but before the next selection.
    /// </summary>
    /// <param name="collapsedPos">Position of the cell that was just collapsed</param>
    /// <param name="collapsedTile">The tile that was placed</param>
    /// <param name="grid">The current grid state</param>
    /// <returns>Cells that need entropy recalculation due to this constraint</returns>
    IEnumerable<Vector2I> GetInvalidatedCells(Vector2I collapsedPos, string collapsedTile, WfcGrid grid);
}
