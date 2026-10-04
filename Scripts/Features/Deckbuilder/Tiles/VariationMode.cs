namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Determines how tile variations are selected during map generation.
/// </summary>
public enum VariationMode
{
    /// <summary>Selects a variant independently for each tile instance.</summary>
    PerInstance = 0,

    /// <summary>Selects one variant at generation start for all instances.</summary>
    PerGeneration = 1,

    /// <summary>Selects a variant from contextual tile and biome information.</summary>
    Contextual = 2
}
