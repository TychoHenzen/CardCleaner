using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Encourages spatial coherence by boosting tiles that extend existing regions.
/// Linear tiles use distance-based repulsion instead.
/// </summary>
public class SpatialCoherenceConstraint : IWfcConstraint, IEntropyInvalidator
{
    private const int SolidFillBitmask = 15;
    private readonly ITileRegistry? _tileRegistry;
    private readonly LinearRepulsionCalculator? _linearRepulsion;
    private readonly SpatialCoherenceStats _stats = new();

    public SpatialCoherenceConstraint(ITileRegistry? tileRegistry = null)
    {
        _tileRegistry = tileRegistry;
        _linearRepulsion = tileRegistry != null ? new LinearRepulsionCalculator(tileRegistry) : null;
    }

    /// <summary>
    /// Target size for coherent regions; larger regions receive a tapered boost.
    /// </summary>
    public int TargetRegionSize { get; set; } = 50;

    /// <summary>
    /// Probability multiplier applied to tiles matching an existing region.
    /// </summary>
    public float BoostFactor { get; set; } = 10.0f;

    /// <summary>
    /// Radius within which linear tiles (hedges) repel each other.
    /// Tiles within this distance apply a penalty that diminishes with distance.
    /// </summary>
    public int LinearRepulsionRadius { get; set; } = 10;

    /// <summary>
    /// Maximum distance-1 penalty factor for nearby same-type linear tiles.
    /// </summary>
    public float LinearRepulsionStrength { get; set; } = 0.5f;

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
        _stats.PrintAndClear();

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
        _stats.RecordCall();

        // Check if this is a linear tile (no bitmask 15) - needs different boost logic
        var isLinearTile = _tileRegistry != null && !HasSolidFillVariant(context.TileId);

        // RegionTracker is only used for grid topologies - mesh topologies use neighbor-count approximation
        if (_regionTracker == null && context.Topology is WfcGrid)
        {
            GD.Print("[SpatialCoherence] RegionTracker is null for grid topology!");
            return 1.0f;
        }

        var scan = CoherenceNeighborScanner.Scan(context, _regionTracker);

        if (!scan.HasAnyCollapsedNeighbor)
            _stats.RecordNoCollapsedNeighbor();

        if (scan.LargestMatchingRegion == 0)
            return 1.0f;

        _stats.RecordMatch(context.TileId, scan.LargestMatchingRegion, isLinearTile);

        // Linear tiles (hedges, paths) repel each other within a radius
        if (isLinearTile)
            return _linearRepulsion!.Calculate(context, CurrentRepulsionSettings());

        var modifier = CalculateRegionBoost(scan.LargestMatchingRegion);
        _stats.RecordBoost(modifier);
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

    private LinearRepulsionSettings CurrentRepulsionSettings() =>
        new(LinearRepulsionRadius, LinearRepulsionStrength, MinModifier);

    private float CalculateRegionBoost(int largestMatchingRegion)
    {
        if (largestMatchingRegion >= TargetRegionSize)
        {
            // Taper off for oversized regions to encourage new regions
            // At 2x target size, boost drops to ~50%
            var oversize = (float)largestMatchingRegion / TargetRegionSize;
            var taperStrength = Mathf.Max(0.0f, 1.0f - (oversize - 1.0f) * 0.5f);
            return 1.0f + taperStrength * BoostFactor;
        }

        // Square root scaling: small regions get meaningful boost, larger regions get more
        // 1-tile: 2.4x, 10-tile: 5.5x, 25-tile: 8.1x, 50-tile: 11x
        // This allows regions to seed while giving larger regions competitive advantage
        var regionProgress = Mathf.Sqrt((float)largestMatchingRegion / TargetRegionSize);
        return 1.0f + regionProgress * BoostFactor;
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
