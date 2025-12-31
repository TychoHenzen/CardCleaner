using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeMapGenerator : IBiomeProvider
{
    private readonly Dictionary<string, int> _biomeStats = new();
    private readonly BiomeDefinition _fallbackBiome;
    private readonly BaselineGradient _gradient;
    private readonly Vector2I _mapSize;
    private readonly BiomeRegistry _registry;

    public BiomeMapGenerator(BiomeRegistry registry, BaselineGradient gradient, Vector2I mapSize)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(gradient);

        _registry = registry;
        _gradient = gradient;
        _mapSize = mapSize;

        _fallbackBiome = CreateFallbackBiome();
    }

    /// <summary>
    ///     Returns biome selection statistics for the last generated map.
    ///     Key = biome ID string, Value = number of tiles assigned to that biome.
    /// </summary>
    public IReadOnlyDictionary<string, int> BiomeStats => _biomeStats;

    public BiomeDefinition GetBiomeAt(Vector2I position)
    {
        var signature = GetSignatureAt(position);
        var biome = _registry.FindClosestBySignature(signature) ?? _fallbackBiome;

        // Track biome statistics
        _biomeStats.TryGetValue(biome.Id, out var count);
        _biomeStats[biome.Id] = count + 1;

        return biome;
    }

    public CardSignature GetSignatureAt(Vector2I position) => _gradient.GetSignatureAt(position, _mapSize);

    public void LogBiomeStats()
    {
        GD.Print("[BiomeMapGenerator] Biome distribution:");
        foreach (var (biomeId, count) in _biomeStats)
        {
            var percentage = (float)count / (_mapSize.X * _mapSize.Y) * 100;
            GD.Print($"  {biomeId}: {count} tiles ({percentage:F1}%)");
        }
    }

    public void ResetStats() => _biomeStats.Clear();

    private static BiomeDefinition CreateFallbackBiome()
    {
        var passable = new TilePool();
        passable.Add("floor");
        var blocked = new TilePool();
        blocked.Add("wall");

        return new BiomeDefinition(
            "plains",
            new CardSignature(),
            passable,
            blocked);
    }
}
