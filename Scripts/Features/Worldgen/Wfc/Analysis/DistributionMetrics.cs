using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Complete distribution analysis of all tile types in a map.
/// </summary>
internal sealed record DistributionMetrics(
    int TotalTiles,
    int UniqueTileTypes,
    TileTypeDistribution[] Distributions
)
{
    /// <summary>
    /// Returns true if all tile types are within the target percentage range.
    /// </summary>
    internal bool IsWithinRange(float minPercent, float maxPercent)
    {
        foreach (var dist in Distributions)
        {
            if (dist.Percentage < minPercent || dist.Percentage > maxPercent)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the maximum percentage of any single tile type.
    /// </summary>
    internal float MaxPercentage => Distributions.Length > 0
        ? Distributions.Max(d => d.Percentage)
        : 0f;

    /// <summary>
    /// Returns the minimum percentage of any tile type.
    /// </summary>
    internal float MinPercentage => Distributions.Length > 0
        ? Distributions.Min(d => d.Percentage)
        : 0f;
}
