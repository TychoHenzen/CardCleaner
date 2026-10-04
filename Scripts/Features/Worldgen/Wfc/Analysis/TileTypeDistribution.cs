namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Distribution statistics for a specific tile type.
/// </summary>
internal sealed record TileTypeDistribution(
    string TileId,
    int TileCount,
    float Percentage,
    int RegionCount
);
