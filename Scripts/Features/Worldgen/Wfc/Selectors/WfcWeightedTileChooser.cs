using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Selectors;

internal static class WfcWeightedTileChooser
{
    internal static string? SelectWeightedTile(
        List<(string tileId, float weight)> weights,
        RandomNumberGenerator rng)
    {
        var totalWeight = 0f;
        foreach (var candidate in weights)
            totalWeight += candidate.weight;

        if (totalWeight <= 0)
            return null;

        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;
        foreach (var candidate in weights)
        {
            cumulative += candidate.weight;
            if (roll <= cumulative)
                return candidate.tileId;
        }

        return weights[^1].tileId;
    }
}
