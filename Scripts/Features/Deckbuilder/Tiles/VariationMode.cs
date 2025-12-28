namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles;

/// <summary>
/// Determines how tile variations are selected during map generation.
/// </summary>
public enum VariationMode
{
    /// <summary>
    /// Each tile instance randomly selects a variant at placement time.
    /// Good for foliage, scattered objects, etc.
    /// </summary>
    PerInstance = 0,

    /// <summary>
    /// One variant is selected at generation start and used for all instances.
    /// Good for themed tiles where consistency across the map is desired.
    /// The selected variant also affects auto-tile coordinates.
    /// </summary>
    PerGeneration = 1
}
