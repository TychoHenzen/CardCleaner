namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling.Validation;

/// <summary>
/// Summary counts reported by <see cref="BitmaskConsistencyValidator.Analyze"/>.
/// </summary>
internal readonly record struct ConsistencyStats(
    int TotalTiles,
    int UniqueTopTerrains,
    int TopTerrainConflicts,
    int BitmaskViolations);
