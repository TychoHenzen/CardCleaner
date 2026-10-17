using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeMapGenerator : IBiomeProvider
{
    private readonly Dictionary<string, int> _biomeStats = new();
    private readonly BiomeDefinition _fallbackBiome;
    private readonly Func<Vector2I, Vector2I, CardSignature> _signatureAt;
    private readonly Vector2I _mapSize;
    private readonly BiomeRegistry _registry;

    /// <param name="signatureAt">Gives the signature at a position of a map of the given size.</param>
    /// <param name="defaultPassableTileId">
    ///     The fallback biome's passable tile; without it the fallback biome's passable pool is empty.
    /// </param>
    /// <param name="defaultSolidTileId">
    ///     The fallback biome's solid tile; without it the fallback biome's blocked pool is empty.
    /// </param>
    public BiomeMapGenerator(
        BiomeRegistry registry,
        Func<Vector2I, Vector2I, CardSignature> signatureAt,
        Vector2I mapSize,
        string? defaultPassableTileId = null,
        string? defaultSolidTileId = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(signatureAt);

        _registry = registry;
        _signatureAt = signatureAt;
        _mapSize = mapSize;

        _fallbackBiome = CreateFallbackBiome(defaultPassableTileId, defaultSolidTileId);
    }

    private static BiomeDefinition CreateFallbackBiome(string? passableTileId, string? solidTileId)
    {
        var passable = new TilePool.Builder();
        if (!string.IsNullOrEmpty(passableTileId))
            passable.Add(passableTileId);

        var blocked = new TilePool.Builder();
        if (!string.IsNullOrEmpty(solidTileId))
            blocked.Add(solidTileId);

        return new BiomeDefinition(
            "plains",
            new CardSignature(),
            passable.Build(),
            blocked.Build());
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

    public CardSignature GetSignatureAt(Vector2I position) => _signatureAt(position, _mapSize);

    public void LogBiomeStats()
    {
        ILog.Print("[BiomeMapGenerator] Biome distribution:");
        foreach (var (biomeId, count) in _biomeStats)
        {
            var percentage = (float)count / (_mapSize.X * _mapSize.Y) * 100;
            ILog.Print($"  {biomeId}: {count} tiles ({percentage:F1}%)");
        }
    }

    public void ResetStats() => _biomeStats.Clear();
}
