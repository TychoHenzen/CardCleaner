using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Selectors;

internal static class WfcWeightedCandidateBuilder
{
    internal static Dictionary<int, string>? BuildCollapsedNeighbors(
        int? cellId,
        IWfcTopology? topology,
        IReadOnlyList<IWfcConstraint> constraints)
    {
        if (!cellId.HasValue || topology == null || constraints.Count == 0)
            return null;

        var collapsedNeighbors = new Dictionary<int, string>(topology.MaxNeighborCount);
        Span<int> neighborBuffer = stackalloc int[topology.MaxNeighborCount];
        var count = topology.GetNeighborsNonAlloc(cellId.Value, neighborBuffer);
        for (var i = 0; i < count; i++)
        {
            var neighborId = neighborBuffer[i];
            var neighborCell = topology.GetCell(neighborId);
            if (neighborCell.IsCollapsed())
                collapsedNeighbors[neighborId] = neighborCell.GetCollapsedTile();
        }

        return collapsedNeighbors;
    }

    internal static Dictionary<string, float> BuildBiomeWeightLookup(BiomeDefinition? biome)
    {
        var lookup = new Dictionary<string, float>();

        if (biome?.PassableTiles == null)
            return lookup;

        foreach (var entry in biome.PassableTiles.Entries)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                lookup[entry.TileId] = entry.Weight;
        }

        return lookup;
    }

    internal static List<(string tileId, float weight)> BuildWeightedCandidates(
        IReadOnlyCollection<string> validTiles,
        WfcTileWeightContext context)
    {
        var candidates = new List<(string tileId, float weight)>(validTiles.Count);
        foreach (var tileId in validTiles)
        {
            var weight = CalculateWeight(tileId, context);
            candidates.Add((tileId, weight));
        }

        return candidates;
    }

    internal static Dictionary<string, float> BuildWeightLookup(
        IReadOnlyCollection<string> validTiles,
        WfcTileWeightContext context)
    {
        var weights = new Dictionary<string, float>(validTiles.Count);
        foreach (var tileId in validTiles)
            weights[tileId] = CalculateWeight(tileId, context);

        return weights;
    }

    private static float CalculateWeight(string tileId, WfcTileWeightContext context)
    {
        var weight = GetBaseWeight(tileId, context);
        if (context.ContinuityTiles != null && context.ContinuityTiles.Contains(tileId))
            weight *= context.ContinuityBiasMultiplier;

        return WfcConstraintModifierApplier.ApplyConstraintModifiers(weight, tileId, context);
    }

    private static float GetBaseWeight(string tileId, WfcTileWeightContext context)
    {
        if (context.UseUniformBaseWeight)
            return 1.0f;

        if (context.BiomeWeights.TryGetValue(tileId, out var biomeWeight))
            return biomeWeight;

        return context.DefaultTileWeight * context.NonBiomeTilePenalty;
    }
}
