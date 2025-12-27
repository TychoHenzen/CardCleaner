using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Transitions;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.BlobGeneration;

/// <summary>
/// Generates coherent "blobs" of same terrain type using noise-based clustering.
/// This reduces the scattered appearance of random tile selection and creates
/// more natural-looking terrain regions.
/// </summary>
public class TerrainBlobGenerator
{
    private readonly BlobGenerationConfig _config;
    private readonly FastNoiseLite _noise;
    private readonly TerrainGroupRegistry _groupRegistry;

    public TerrainBlobGenerator(BlobGenerationConfig config, TerrainGroupRegistry groupRegistry, int seed)
    {
        _config = config;
        _groupRegistry = groupRegistry;

        _noise = new FastNoiseLite();
        _noise.Seed = seed;
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noise.Frequency = _config.NoiseScale;
        _noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
        _noise.FractalOctaves = 3;
    }

    /// <summary>
    /// Select a terrain tile from the biome's pool, influenced by noise
    /// to create coherent regions of the same terrain type.
    /// </summary>
    /// <param name="position">The map position</param>
    /// <param name="pool">The tile pool to select from</param>
    /// <param name="rng">Random number generator for fallback selection</param>
    /// <returns>Selected tile ID</returns>
    public string? SelectTileWithClustering(Vector2I position, TilePool pool, RandomNumberGenerator rng)
    {
        if (!_config.Enabled || pool.IsEmpty)
            return pool.SelectRandom(rng);

        // Group tiles by their terrain group
        var tilesByGroup = GroupTilesByTerrainGroup(pool);
        if (tilesByGroup.Count == 0)
            return pool.SelectRandom(rng);

        // Use noise to select which terrain group to prefer at this position
        var noiseValue = _noise.GetNoise2D(position.X, position.Y);

        // Map noise value (-1 to 1) to group index
        // Add some randomness based on cluster strength
        var normalizedNoise = (noiseValue + 1f) / 2f; // 0 to 1
        var randomFactor = rng.Randf() * (1f - _config.ClusterStrength);
        var finalValue = normalizedNoise * _config.ClusterStrength + randomFactor;

        // Select the terrain group based on the combined noise+random value
        var selectedGroup = SelectGroupByValue(tilesByGroup, finalValue);

        // Select a random tile from the chosen group
        if (selectedGroup != null && tilesByGroup.TryGetValue(selectedGroup, out var tilesInGroup) && tilesInGroup.Count > 0)
        {
            var index = rng.RandiRange(0, tilesInGroup.Count - 1);
            return tilesInGroup[index];
        }

        // Fallback to random selection
        return pool.SelectRandom(rng);
    }

    /// <summary>
    /// Group tiles by their terrain group for clustering
    /// </summary>
    private Dictionary<string, List<string>> GroupTilesByTerrainGroup(TilePool pool)
    {
        var result = new Dictionary<string, List<string>>();

        foreach (var entry in pool.Entries)
        {
            var tileId = entry.TileId;
            var group = _groupRegistry.GetGroupForTile(tileId);
            var groupId = group?.Id ?? "_ungrouped";

            if (!result.ContainsKey(groupId))
                result[groupId] = [];

            result[groupId].Add(tileId);
        }

        return result;
    }

    /// <summary>
    /// Select a terrain group based on a normalized value (0-1)
    /// </summary>
    private static string? SelectGroupByValue(Dictionary<string, List<string>> groups, float value)
    {
        if (groups.Count == 0)
            return null;

        var groupList = groups.Keys.ToList();
        var index = Mathf.FloorToInt(value * groupList.Count);
        index = Mathf.Clamp(index, 0, groupList.Count - 1);

        return groupList[index];
    }
}
