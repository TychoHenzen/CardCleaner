namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Unified constraint interface for WFC tile selection.
/// Replaces separate IHardConstraint and ISoftModifier with single probability-based interface.
/// </summary>
/// <remarks>
/// Return value semantics:
/// <list type="bullet">
///   <item><description>0.0: Hard ban - tile is eliminated from possibilities</description></item>
///   <item><description>0.0-1.0: Soft penalty - reduces probability proportionally</description></item>
///   <item><description>1.0: Neutral - no effect on probability</description></item>
///   <item><description>&gt;1.0: Boost - increases probability proportionally</description></item>
/// </list>
///
/// Constraints are applied multiplicatively: final_weight = base_weight * product(modifiers)
/// </remarks>
public interface IWfcConstraint
{
    /// <summary>
    /// Calculates the probability modifier for placing a tile at a position.
    /// </summary>
    /// <param name="context">Position, tile, grid, and optional RNG context</param>
    /// <returns>Probability modifier (0.0 = ban, 1.0 = neutral, &gt;1.0 = boost)</returns>
    float GetProbabilityModifier(WfcConstraintContext context);
}
