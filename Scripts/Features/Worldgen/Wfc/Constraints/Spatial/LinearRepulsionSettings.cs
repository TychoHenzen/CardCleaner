namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Tuning values for the repulsion between nearby linear tiles.
/// </summary>
internal readonly record struct LinearRepulsionSettings(int Radius, float Strength, float MinModifier);
