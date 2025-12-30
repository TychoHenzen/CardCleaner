namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Interface for variant weight modification rules.
/// Modifiers apply multiplicative adjustments to variant weights in-place
/// after a base tile has been selected.
/// </summary>
public interface IVariantWeightModifier
{
    /// <summary>
    /// Apply weight modifications to the context's VariantWeights array in-place.
    /// Implementations should multiply existing weights rather than replace them.
    /// </summary>
    /// <param name="context">The variant selection context containing position, biome, tile info, and weights.</param>
    void ApplyModifier(VariantSelectionContext context);
}
