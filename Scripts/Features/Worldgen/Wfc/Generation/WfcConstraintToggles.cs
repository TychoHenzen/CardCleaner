namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Which of the optional base constraints are switched on for a generation.
/// </summary>
internal readonly record struct WfcConstraintToggles(
    bool DiminishingReturns,
    bool SpatialCoherence,
    bool Compactness);
