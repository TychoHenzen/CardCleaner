using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeRegistry
{
    private readonly Dictionary<string, BiomeDefinition> _biomes = new();

    public int Count => _biomes.Count;

    public void Register(BiomeDefinition biome) => _biomes[biome.Id] = biome;

    public BiomeDefinition? GetBiome(string biomeId) => _biomes.GetValueOrDefault(biomeId);

    public IEnumerable<BiomeDefinition> GetAllBiomes() => _biomes.Values;

    public IEnumerable<string> GetAllBiomeIds() => _biomes.Keys;

    public BiomeDefinition? FindClosestBySignature(CardSignature signature)
    {
        if (_biomes.Count == 0)
            return null;

        BiomeDefinition? closest = null;
        var closestDistance = float.MaxValue;

        foreach (var biome in _biomes.Values)
        {
            var distance = signature.DistanceTo(biome.AffinitySignature);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = biome;
            }
        }

        return closest;
    }

    public void Clear() => _biomes.Clear();

    /// <summary>
    /// Load biomes from tiles.json data file
    /// </summary>
    public void LoadFromData(string? path = null)
    {
        var biomeData = TileDataLoader.LoadBiomes(path);
        if (biomeData.Count == 0)
        {
            ILog.Print("[BiomeRegistry] No biomes loaded from data");
            return;
        }

        foreach (var (biomeId, data) in biomeData)
        {
            var biomeDefinition = BiomeDefinition.FromData(biomeId, data);
            Register(biomeDefinition);
        }

        ILog.Print($"[BiomeRegistry] Registered {_biomes.Count} biomes from data");
    }

    /// <summary>
    /// Register all biomes from tiles.json data file.
    /// </summary>
    public void RegisterDefaultBiomes() => LoadFromData();
}
