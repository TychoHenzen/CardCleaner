using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Selectors;

internal static class WfcConstraintModifierApplier
{
    internal static float ApplyConstraintModifiers(float weight, string tileId, WfcTileWeightContext context)
    {
        if (!context.CellId.HasValue || context.Topology == null || context.Constraints.Count == 0)
            return weight;

        var constraintContext = WfcConstraintContext.Create(
            context.CellId.Value,
            tileId,
            context.Topology,
            context.Rng,
            context.CollapsedNeighbors!,
            context.CollapsedWindowNeighbors!);

        foreach (var constraint in context.Constraints)
        {
            var modifier = constraint.GetProbabilityModifier(constraintContext);
            if (modifier == 0f)
                return 0f;

            weight *= modifier;
        }

        return weight;
    }
}
