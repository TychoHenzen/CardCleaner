using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
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

    public TerrainBlobGenerator(BlobGenerationConfig config, int seed)
    {
        _config = config;

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

        // Group tiles by base ID (e.g., "grass_1", "grass_2" -> "grass")
        var tilesByGroup = GroupTilesByBaseId(pool);
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
        if (selectedGroup != null && tilesByGroup.TryGetValue(selectedGroup, out var tilesInGroup) &&
            tilesInGroup.Count > 0)
        {
            var index = rng.RandiRange(0, tilesInGroup.Count - 1);
            return tilesInGroup[index];
        }

        // Fallback to random selection
        return pool.SelectRandom(rng);
    }

    /// <summary>
    /// Group tiles by their base ID (prefix before underscore or numeric suffix)
    /// e.g., "grass_1", "grass_2" -> "grass"; "dirt_path" -> "dirt"
    /// </summary>
    private static Dictionary<string, List<string>> GroupTilesByBaseId(TilePool pool)
    {
        var result = new Dictionary<string, List<string>>();

        foreach (var entry in pool.Entries)
        {
            var tileId = entry.TileId;
            var baseId = GetBaseId(tileId);

            if (!result.ContainsKey(baseId))
                result[baseId] = [];

            result[baseId].Add(tileId);
        }

        return result;
    }

    /// <summary>
    ///     Extract the base ID from a tile ID (e.g., "grass_1" -> "grass", "stone" -> "stone")
    /// </summary>
    private static string GetBaseId(string tileId)
    {
        // Find the last underscore followed by digits
        var lastUnderscore = tileId.LastIndexOf('_');
        if (lastUnderscore > 0 && lastUnderscore < tileId.Length - 1)
        {
            var suffix = tileId[(lastUnderscore + 1)..];
            if (int.TryParse(suffix, out _))
                // It's a numeric variant like "grass_1"
                return tileId[..lastUnderscore];
        }

        // No numeric suffix, use the full ID
        return tileId;
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
