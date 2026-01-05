using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Encourages spatial coherence by boosting tiles that form larger contiguous regions.
/// During WFC collapse, tracks regions of identical tiles and applies probability modifiers
/// to encourage or discourage region growth based on target size.
/// </summary>
/// <remarks>
/// Region tracking strategy:
/// - Scans collapsed neighbors (4-directional) to detect existing regions
/// - Tiles matching larger regions receive boosts proportional to (regionSize / targetSize)
/// - Prevents fragmentation by penalizing tiles that would create isolated pockets
///
/// Probability calculation:
/// - If tile matches neighbor region: modifier = 1.0 + (regionRatio * BoostFactor)
/// - If tile would fragment region: modifier = MinModifier
/// - If no neighbors collapsed: modifier = 1.0 (neutral)
/// </remarks>
public class SpatialCoherenceConstraint : IWfcConstraint
{
    /// <summary>
    /// Target size for coherent regions (in tiles).
    /// Regions approaching this size receive maximum boost.
    /// </summary>
    public int TargetRegionSize { get; set; } = 40;

    /// <summary>
    /// Factor controlling how much regional coherence affects probability.
    /// Default 2.5 means: region at target size → 3.5x probability boost.
    /// Balanced to compete with BiomeAffinityConstraint (3.0x) and ContinuityBias (2.0x).
    /// </summary>
    public float BoostFactor { get; set; } = 2.5f;

    /// <summary>
    /// Minimum probability modifier to prevent complete tile elimination.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    private RegionTracker? _regionTracker;

    /// <summary>
    /// Initializes region tracking for a new WFC generation.
    /// Must be called before WFC collapse begins.
    /// </summary>
    public void Reset(int width, int height)
    {
        _regionTracker ??= new RegionTracker(width, height);
        _regionTracker.Reset();
    }

    /// <summary>
    /// Updates region tracking when a tile collapses.
    /// Should be called by WFC solver after each collapse.
    /// </summary>
    public void OnTileCollapsed(Vector2I position, string tileId, WfcGrid grid)
    {
        _regionTracker?.OnTileCollapsed(position, tileId, grid);
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        if (_regionTracker == null)
            return 1.0f;

        var neighbors = context.Grid.GetNeighbors(context.Position);
        var largestMatchingRegion = 0;

        foreach (var neighbor in neighbors)
        {
            var neighborTile = context.Grid.GetCollapsedTileAt(neighbor);
            if (neighborTile == context.TileId)
            {
                var regionSize = _regionTracker.GetRegionSize(neighbor);
                if (regionSize > largestMatchingRegion)
                    largestMatchingRegion = regionSize;
            }
        }

        if (largestMatchingRegion == 0)
            return 1.0f;

        var minThreshold = TargetRegionSize * 0.75f;
        var maxThreshold = TargetRegionSize * 1.25f;

        float strength;
        if (largestMatchingRegion < minThreshold)
            strength = Mathf.Clamp(largestMatchingRegion / minThreshold, 0.1f, 1.0f);
        else if (largestMatchingRegion > maxThreshold)
            strength = Mathf.Max(0.0f, 2.0f - (largestMatchingRegion / maxThreshold));
        else
            strength = 1.0f;

        var modifier = 1.0f + strength * BoostFactor;
        return Mathf.Max(MinModifier, modifier);
    }
}
