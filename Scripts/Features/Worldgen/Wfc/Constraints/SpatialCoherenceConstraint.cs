using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
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
/// Probability calculation for regular tiles:
/// - If tile matches any neighbor region below target: modifier = 1.0 + BoostFactor
/// - If tile matches oversized region: modifier tapers toward 1.0
/// - If no neighbors collapsed: modifier = 1.0 (neutral)
///
/// Linear tiles (without bitmask 15, e.g., hedges) use repulsion instead of attraction:
/// - Same-type tiles within LinearRepulsionRadius apply a distance-weighted penalty
/// - Closer tiles apply stronger penalties, diminishing with distance
/// - This creates sparse, spread-out hedge structures instead of dense clusters
/// The NoSolidFillConstraint separately ensures they remain 1-tile wide.
/// </remarks>
public class SpatialCoherenceConstraint : IWfcConstraint, IEntropyInvalidator
{
    private const int SolidFillBitmask = 15;
    private readonly ITileRegistry? _tileRegistry;

    public SpatialCoherenceConstraint(ITileRegistry? tileRegistry = null)
    {
        _tileRegistry = tileRegistry;
    }
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
    /// Radius within which linear tiles (hedges) repel each other.
    /// Tiles within this distance apply a penalty that diminishes with distance.
    /// </summary>
    public int LinearRepulsionRadius { get; set; } = 10;

    /// <summary>
    /// Maximum penalty factor for linear tiles at distance 1.
    /// A value of 0.9 means a same-type tile at distance 1 reduces probability by 90%.
    /// Penalty diminishes linearly with distance up to LinearRepulsionRadius.
    /// </summary>
    public float LinearRepulsionStrength { get; set; } = 0.5f;

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

        // Check if this is a linear tile (no bitmask 15) - needs different boost logic
        var isLinearTile = _tileRegistry != null && !HasSolidFillVariant(context.TileId);

        // RegionTracker is only used for grid topologies - mesh topologies use neighbor-count approximation
        var isGridTopology = context.Topology is WfcGrid;
        if (_regionTracker == null && isGridTopology)
        {
            GD.Print("[SpatialCoherence] RegionTracker is null for grid topology!");
            return 1.0f;
        }

        var largestMatchingRegion = 0;
        var hasAnyCollapsedNeighbor = false;
        var sameTypeNeighborCount = 0;

        // Use precomputed neighbor info if available (optimization)
        if (context.NeighborInfo.HasValue)
        {
            var neighborInfo = context.NeighborInfo.Value;
            hasAnyCollapsedNeighbor = neighborInfo.HasCollapsedNeighbor;
            sameTypeNeighborCount = neighborInfo.SameTypeCount;

            // For region tracking, convert cell IDs to positions (grid-specific)
            if (context.Topology is WfcGrid grid)
            {
                foreach (var kvp in neighborInfo.Neighbors)
                {
                    if (kvp.Value == context.TileId)
                    {
                        var neighborPos = grid.CellIdToPosition(kvp.Key);
                        var regionSize = _regionTracker.GetRegionSize(neighborPos);
                        if (regionSize > largestMatchingRegion)
                            largestMatchingRegion = regionSize;
                    }
                }
            }
            else
            {
                // Non-grid topology: use same-type count as region size estimate
                // This is a simplification for mesh topologies
                if (sameTypeNeighborCount > 0)
                    largestMatchingRegion = sameTypeNeighborCount;
            }
        }
        else if (context.Topology is WfcGrid gridFallback)
        {
            // Fallback: iterate neighbors directly (for grid topology)
            var position = gridFallback.CellIdToPosition(context.CellId);
            foreach (var neighbor in gridFallback.GetNeighbors(position))
            {
                var neighborTile = gridFallback.GetCollapsedTileAt(neighbor);
                if (neighborTile != null)
                {
                    hasAnyCollapsedNeighbor = true;
                    if (neighborTile == context.TileId)
                    {
                        sameTypeNeighborCount++;
                        var regionSize = _regionTracker.GetRegionSize(neighbor);
                        if (regionSize > largestMatchingRegion)
                            largestMatchingRegion = regionSize;
                    }
                }
            }
        }
        else
        {
            // Non-grid fallback: iterate neighbors via topology
            foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
            {
                var neighborTile = context.Topology.GetCollapsedTileAt(neighborId);
                if (neighborTile != null)
                {
                    hasAnyCollapsedNeighbor = true;
                    if (neighborTile == context.TileId)
                        sameTypeNeighborCount++;
                }
            }
            if (sameTypeNeighborCount > 0)
                largestMatchingRegion = sameTypeNeighborCount;
        }

        if (!hasAnyCollapsedNeighbor)
            _callsNoCollapsedNeighbor++;

        if (largestMatchingRegion == 0)
            return 1.0f;

        _callsWithMatch++;

        // Debug: log first few boosts to verify spatial coherence is working
        if (_callsWithMatch <= 5)
        {
            GD.Print($"[SpatialCoherence] Match #{_callsWithMatch}: tile={context.TileId}, regionSize={largestMatchingRegion}, isLinear={isLinearTile}");
        }

        float modifier;

        if (isLinearTile)
        {
            // Linear tiles (hedges, paths) repel each other within a radius
            // This creates sparse, spread-out structures instead of dense clusters
            return CalculateLinearRepulsion(context);
        }
        else if (largestMatchingRegion >= TargetRegionSize)
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

    /// <summary>
    /// Returns cells affected by region growth beyond immediate neighbors.
    /// When a cell is collapsed, cells bordering the same region have their entropy affected.
    /// </summary>
    public IEnumerable<Vector2I> GetInvalidatedCells(Vector2I collapsedPos, string collapsedTile, WfcGrid grid)
    {
        // When cell P is collapsed with tile T, the region it joins grows.
        // All uncollapsed cells bordering that region are affected.
        // Approximation: return 2-hop neighbors (neighbors of same-type neighbors)

        var invalidated = new HashSet<Vector2I>();

        // Check each 4-way neighbor
        foreach (var neighbor in grid.GetNeighbors(collapsedPos))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighbor);
            if (neighborTile == collapsedTile)
            {
                // This neighbor is in the same region - its neighbors are affected
                foreach (var twoHop in grid.GetNeighbors(neighbor))
                {
                    if (twoHop != collapsedPos && !grid.GetCell(twoHop).IsCollapsed())
                    {
                        invalidated.Add(twoHop);
                    }
                }
            }
        }

        return invalidated;
    }

    /// <summary>
    /// Calculates repulsion penalty for linear tiles based on nearby same-type tiles.
    /// </summary>
    private float CalculateLinearRepulsion(WfcConstraintContext context)
    {
        // Only works for grid topologies (requires coordinate-based distance)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        var totalPenalty = 0.0f;
        var pos = grid.CellIdToPosition(context.CellId);

        // Scan within repulsion radius
        for (var dy = -LinearRepulsionRadius; dy <= LinearRepulsionRadius; dy++)
        {
            for (var dx = -LinearRepulsionRadius; dx <= LinearRepulsionRadius; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var checkX = pos.X + dx;
                var checkY = pos.Y + dy;

                // Bounds check
                if (checkX < 0 || checkY < 0 || checkX >= grid.Width || checkY >= grid.Height)
                    continue;

                var cell = grid.GetCell(new Vector2I(checkX, checkY));
                if (!cell.IsCollapsed())
                    continue;

                // Check if same tile type (using terrain type comparison for auto-tiles)
                var collapsedTile = cell.GetCollapsedTile();
                if (!_tileRegistry!.AreSameTerrainType(context.TileId, collapsedTile))
                    continue;

                // Calculate distance and penalty (Chebyshev distance for grid)
                var distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                if (distance > LinearRepulsionRadius)
                    continue;

                // Penalty diminishes linearly with distance
                // At distance 1: full penalty, at radius: zero penalty
                var distanceFactor = 1.0f - (float)(distance - 1) / LinearRepulsionRadius;
                totalPenalty += LinearRepulsionStrength * distanceFactor;
            }
        }

        // Convert accumulated penalty to modifier (clamped to MinModifier)
        var modifier = Mathf.Max(MinModifier, 1.0f - totalPenalty);
        return modifier;
    }

    private bool HasSolidFillVariant(string tileId)
    {
        var tile = _tileRegistry?.GetTile(tileId);
        if (tile == null)
            return true; // Assume has solid fill if we can't check

        // Non-auto-tiles don't have variants, treat as having solid fill
        if (!tile.HasAutoTileVariants)
            return true;

        var variants = tile.AutoTileVariants;
        if (variants == null || variants.Length <= SolidFillBitmask)
            return false;

        return variants[SolidFillBitmask].HasValue;
    }
}
