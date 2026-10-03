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
    /// <summary>Returns additional cells whose entropy is affected by a collapse.</summary>
    IEnumerable<Vector2I> GetInvalidatedCells(Vector2I collapsedPos, string collapsedTile, WfcGrid grid);
}
