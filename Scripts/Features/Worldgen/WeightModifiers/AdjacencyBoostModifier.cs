using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Weight modifier that boosts tiles matching adjacent already-placed tiles.
/// Creates clustering behavior where similar tiles tend to group together.
/// </summary>
public sealed class AdjacencyBoostModifier : IWeightModifier
{
    private readonly Func<string, string>? _getTileGroup;

    /// <summary>
    /// Boost multiplier per matching neighbor (default 1.5x).
    /// With 4 matching neighbors: 1.5^4 = 5.06x boost.
    /// </summary>
    public float BoostPerNeighbor { get; set; } = 1.5f;

    /// <summary>
    /// Create modifier with optional tile grouping function.
    /// </summary>
    /// <param name="getTileGroup">
    /// Function to get tile group from tile ID. Tiles in the same group
    /// are considered matching. If null, uses exact tile ID matching.
    /// Example: "grass_1" and "grass_2" could both return "grass".
    /// </param>
    public AdjacencyBoostModifier(Func<string, string>? getTileGroup = null)
    {
        _getTileGroup = getTileGroup;
    }

    public void ApplyModifier(TileSelectionContext context)
    {
        var neighborTiles = context.GetNeighborTiles();
        if (neighborTiles.Count == 0)
            return;

        // Count neighbor groups
        var neighborGroups = new Dictionary<string, int>();
        foreach (var (_, tileId) in neighborTiles)
        {
            var group = GetGroup(tileId);
            neighborGroups.TryGetValue(group, out var count);
            neighborGroups[group] = count + 1;
        }

        // Boost tiles that match neighbor groups
        var weights = context.Weights;
        foreach (var tileId in new List<string>(weights.Keys))
        {
            var group = GetGroup(tileId);
            if (neighborGroups.TryGetValue(group, out var matchCount))
            {
                // Apply exponential boost based on number of matching neighbors
                var boost = MathF.Pow(BoostPerNeighbor, matchCount);
                weights[tileId] *= boost;
            }
        }
    }

    private string GetGroup(string tileId)
    {
        return _getTileGroup?.Invoke(tileId) ?? tileId;
    }

    /// <summary>
    /// Create a grouping function that strips numeric suffixes.
    /// "grass_1", "grass_2" -> "grass"
    /// </summary>
    public static Func<string, string> StripNumericSuffix => tileId =>
    {
        var lastUnderscore = tileId.LastIndexOf('_');
        if (lastUnderscore <= 0 || lastUnderscore >= tileId.Length - 1)
            return tileId;

        var suffix = tileId.AsSpan(lastUnderscore + 1);
        return int.TryParse(suffix, out _) ? tileId[..lastUnderscore] : tileId;
    };
}
