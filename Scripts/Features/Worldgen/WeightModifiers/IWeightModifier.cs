namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Interface for weight modification rules in tile selection.
/// Modifiers apply multiplicative adjustments to tile weights in-place.
/// </summary>
public interface IWeightModifier
{
    /// <summary>
    /// Apply weight modifications to the context's Weights dictionary in-place.
    /// Implementations should multiply existing weights rather than replace them.
    /// </summary>
    /// <param name="context">The tile selection context containing position, biome, and weights.</param>
    void ApplyModifier(TileSelectionContext context);
}
