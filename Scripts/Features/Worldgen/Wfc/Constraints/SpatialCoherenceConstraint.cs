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
/// - Boost scales gradually with region size up to maxThreshold (1.25 × target)
/// - Boost tapers off for oversized regions to encourage tile diversity
///
/// Probability calculation:
/// - If tile matches neighbor region: modifier = 1.0 + (size/target) × BoostFactor
/// - If tile matches oversized region: modifier tapers toward 1.0
/// - If no neighbors collapsed: modifier = 1.0 (neutral)
/// </remarks>
public class SpatialCoherenceConstraint : IWfcConstraint
{
    /// <summary>
    /// Target size for coherent regions (in tiles).
    /// Regions approaching this size receive maximum boost.
    /// For 89-tile maps, 20 tiles = ~22% per region, allowing 3-5 regions.
    /// </summary>
    public int TargetRegionSize { get; set; } = 20;

    /// <summary>
    /// Factor controlling how much regional coherence affects probability.
    /// With BoostFactor=8.0, matching tiles get meaningful boost for region growth.
    /// Works with DiminishingReturns (decay 0.2) to balance growth:
    /// - Size 5:  9.0x coherence × 0.5 diminishing = 4.5x net boost
    /// - Size 15: 9.0x coherence × 0.25 diminishing = 2.25x net boost (target region)
    /// - Size 25: Taper begins, reducing coherence boost to encourage new regions
    /// </summary>
    public float BoostFactor { get; set; } = 8.0f;

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

        // Regions above maxThreshold start tapering off to encourage diversity
        var maxThreshold = TargetRegionSize * 1.25f;  // 20 * 1.25 = 25

        float modifier;
        if (largestMatchingRegion > maxThreshold)
        {
            // Taper off for oversized regions - stronger taper to encourage new regions
            var oversize = largestMatchingRegion / maxThreshold;
            var taperStrength = Mathf.Max(0.0f, 1.0f - (oversize - 1.0f) * 0.5f);
            modifier = 1.0f + taperStrength * BoostFactor;
        }
        else
        {
            // Gradual boost based on region size - no minimum floor
            // Small regions get small boost, larger regions get stronger boost up to target
            var growthProgress = (float)largestMatchingRegion / TargetRegionSize;
            modifier = 1.0f + growthProgress * BoostFactor;
        }

        return Mathf.Max(MinModifier, modifier);
    }
}
