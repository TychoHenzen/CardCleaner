using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeRegistry
{
    private readonly Dictionary<BiomeType, BiomeDefinition> _biomes = new();

    public int Count => _biomes.Count;

    public void Register(BiomeDefinition biome) => _biomes[biome.Type] = biome;

    public BiomeDefinition? GetBiome(BiomeType type) => _biomes.GetValueOrDefault(type);

    public IEnumerable<BiomeDefinition> GetAllBiomes() => _biomes.Values;

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
    /// Register default biomes from tiles.json (backwards compatibility)
    /// </summary>
    public void RegisterDefaultBiomes()
    {
        LoadFromData();
    }
}
