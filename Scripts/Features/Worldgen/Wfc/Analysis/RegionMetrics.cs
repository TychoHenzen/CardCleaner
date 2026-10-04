namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Statistics about contiguous regions in a tile map.
/// </summary>
internal sealed record RegionMetrics(
    int RegionCount,
    int MinSize,
    int MaxSize,
    float AverageSize,
    float PercentInLargeRegions,
    int TilesInLargeRegions,
    int TotalTiles
);
