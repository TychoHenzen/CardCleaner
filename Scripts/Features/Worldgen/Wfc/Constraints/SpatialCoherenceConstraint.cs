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
/// - Tiles matching ANY region receive full boost up to maxThreshold (1.25 × target)
/// - Boost tapers off for oversized regions to prevent single-tile-type domination
///
/// Probability calculation:
/// - If tile matches neighbor region (size ≤ maxThreshold): modifier = 1.0 + BoostFactor
/// - If tile matches oversized region (size > maxThreshold): modifier tapers down
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
    /// Increased to 20.0 to overcome base weight differences between competing tile types.
    /// With BoostFactor=20.0, matching tiles get 21.0x boost (1.0 + 20.0).
    /// Works with DiminishingReturns (decay 0.05) to balance growth:
    /// - Size 10: 21.0x coherence × 0.67 diminishing = 14.1x net boost
    /// - Size 40: 21.0x coherence × 0.33 diminishing = 7.0x net boost
    /// - Size 50: Taper begins, reducing coherence boost to prevent domination
    /// This strong boost ensures extending existing regions wins over fragmenting with different tiles.
    /// </summary>
    public float BoostFactor { get; set; } = 20.0f;

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

        // Strong minimum boost to overcome tile variety (89 tiles need ~50x boost minimum)
        // Then scale up slightly for larger regions to favor growing big regions
        var maxThreshold = TargetRegionSize * 1.25f;  // 40 * 1.25 = 50

        float modifier;
        if (largestMatchingRegion > maxThreshold)
        {
            // Taper off for oversized regions
            var taperStrength = Mathf.Max(0.0f, 2.0f - (largestMatchingRegion / maxThreshold));
            modifier = 1.0f + taperStrength * BoostFactor;
        }
        else
        {
            // Strong MINIMUM boost (50x) plus scaling bonus up to BoostFactor (20x more)
            // This ensures even size-1 regions have enough boost to grow
            const float minBoost = 50.0f;
            var scalingBonus = (largestMatchingRegion / TargetRegionSize) * BoostFactor;
            modifier = minBoost + scalingBonus;
        }

        return Mathf.Max(MinModifier, modifier);
    }
}
