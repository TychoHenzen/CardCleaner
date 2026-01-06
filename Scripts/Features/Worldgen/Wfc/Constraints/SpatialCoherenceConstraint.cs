using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Encourages spatial coherence by boosting tiles that extend existing regions.
/// During WFC collapse, tracks regions of identical tiles and applies probability modifiers
/// to encourage region growth up to target size.
/// </summary>
/// <remarks>
/// Region tracking strategy:
/// - Scans collapsed neighbors (4-directional) to detect existing regions
/// - Full boost given for ANY matching neighbor (encourages region growth from size 1)
/// - Boost tapers off for oversized regions to encourage tile diversity
///
/// Probability calculation:
/// - If tile matches any neighbor region below target: modifier = 1.0 + BoostFactor
/// - If tile matches oversized region: modifier tapers toward 1.0
/// - If no neighbors collapsed: modifier = 1.0 (neutral)
/// </remarks>
public class SpatialCoherenceConstraint : IWfcConstraint
{
    /// <summary>
    /// Target size for coherent regions (in tiles).
    /// Regions below this receive full boost; regions above start tapering.
    /// For ~3600-tile maps, 50 tiles = ~1.4% per region, allowing 20-70 regions.
    /// </summary>
    public int TargetRegionSize { get; set; } = 50;

    /// <summary>
    /// Factor controlling how much regional coherence affects probability.
    /// With BoostFactor=10.0, matching tiles get 11x weight boost.
    /// This ensures extending a region strongly dominates over starting new ones.
    /// </summary>
    public float BoostFactor { get; set; } = 500.0f;

    /// <summary>
    /// Minimum probability modifier to prevent complete tile elimination.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    private RegionTracker? _regionTracker;

    // Diagnostic counters
    private int _totalCalls;
    private int _callsWithMatch;
    private int _callsNoCollapsedNeighbor;
    private float _totalBoostApplied;

    /// <summary>
    /// Initializes region tracking for a new WFC generation.
    /// Must be called before WFC collapse begins.
    /// </summary>
    public void Reset(int width, int height)
    {
        // Print stats from previous run
        if (_totalCalls > 0)
        {
            var matchRate = _callsWithMatch * 100f / _totalCalls;
            var avgBoost = _callsWithMatch > 0 ? _totalBoostApplied / _callsWithMatch : 0;
            GD.Print($"[SpatialCoherence] Stats: {_totalCalls} calls, {_callsWithMatch} with match ({matchRate:F1}%), {_callsNoCollapsedNeighbor} no collapsed neighbor, avg boost {avgBoost:F2}x");
        }

        _regionTracker ??= new RegionTracker(width, height);
        _regionTracker.Reset();

        // Reset counters
        _totalCalls = 0;
        _callsWithMatch = 0;
        _callsNoCollapsedNeighbor = 0;
        _totalBoostApplied = 0;
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
        _totalCalls++;

        if (_regionTracker == null)
        {
            GD.Print("[SpatialCoherence] RegionTracker is null!");
            return 1.0f;
        }

        var largestMatchingRegion = 0;
        var hasAnyCollapsedNeighbor = false;

        // Use precomputed neighbor info if available (optimization)
        if (context.NeighborInfo.HasValue)
        {
            var neighborInfo = context.NeighborInfo.Value;
            hasAnyCollapsedNeighbor = neighborInfo.HasCollapsedNeighbor4;

            foreach (var kvp in neighborInfo.Neighbors4)
            {
                if (kvp.Value == context.TileId)
                {
                    var regionSize = _regionTracker.GetRegionSize(kvp.Key);
                    if (regionSize > largestMatchingRegion)
                        largestMatchingRegion = regionSize;
                }
            }
        }
        else
        {
            // Fallback: iterate neighbors directly (for backward compatibility)
            foreach (var neighbor in context.Grid.GetNeighbors(context.Position))
            {
                var neighborTile = context.Grid.GetCollapsedTileAt(neighbor);
                if (neighborTile != null)
                {
                    hasAnyCollapsedNeighbor = true;
                    if (neighborTile == context.TileId)
                    {
                        var regionSize = _regionTracker.GetRegionSize(neighbor);
                        if (regionSize > largestMatchingRegion)
                            largestMatchingRegion = regionSize;
                    }
                }
            }
        }

        if (!hasAnyCollapsedNeighbor)
            _callsNoCollapsedNeighbor++;

        if (largestMatchingRegion == 0)
            return 1.0f;

        _callsWithMatch++;

        float modifier;
        if (largestMatchingRegion >= TargetRegionSize)
        {
            // Taper off for oversized regions to encourage new regions
            // At 2x target size, boost drops to ~50%
            var oversize = (float)largestMatchingRegion / TargetRegionSize;
            var taperStrength = Mathf.Max(0.0f, 1.0f - (oversize - 1.0f) * 0.5f);
            modifier = 1.0f + taperStrength * BoostFactor;
        }
        else
        {
            // Square root scaling: small regions get meaningful boost, larger regions get more
            // 1-tile: 2.4x, 10-tile: 5.5x, 25-tile: 8.1x, 50-tile: 11x
            // This allows regions to seed while giving larger regions competitive advantage
            var regionProgress = Mathf.Sqrt((float)largestMatchingRegion / TargetRegionSize);
            modifier = 1.0f + regionProgress * BoostFactor;
        }

        _totalBoostApplied += modifier;
        return Mathf.Max(MinModifier, modifier);
    }
}
