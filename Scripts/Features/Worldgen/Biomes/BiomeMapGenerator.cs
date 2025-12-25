using System;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeMapGenerator : IBiomeProvider
{
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

    public BiomeDefinition GetBiomeAt(Vector2I position)
    {
        var signature = GetSignatureAt(position);
        return _registry.FindClosestBySignature(signature) ?? _fallbackBiome;
    }

    public CardSignature GetSignatureAt(Vector2I position) => _gradient.GetSignatureAt(position, _mapSize);

    private static BiomeDefinition CreateFallbackBiome()
    {
        var passable = new TilePool();
        passable.Add("floor");
        var blocked = new TilePool();
        blocked.Add("wall");

        return new BiomeDefinition(
            BiomeType.Plains,
            new CardSignature(),
            passable,
            blocked);
    }
}
