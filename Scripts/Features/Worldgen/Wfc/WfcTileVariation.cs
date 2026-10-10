namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// A tile's variation group as the WFC constraints read it.
/// </summary>
/// <param name="BaseName">The group's base name, for example "grass".</param>
/// <param name="IsPerGeneration">True when the group picks one variant for the whole map.</param>
/// <param name="Density">The group's maximum weight, which sets its density.</param>
/// <param name="NormalizedWeight">
/// The tile's weight relative to the group maximum. Zero for PerGeneration groups, which do not use it.
/// </param>
public readonly record struct WfcTileVariation(
    string BaseName,
    bool IsPerGeneration,
    float Density,
    float NormalizedWeight);
