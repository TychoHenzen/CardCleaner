using System;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Front door for the biome provider a map uses to pick each tile's biome from its signature.
/// </summary>
internal static class BiomeMaps
{
    internal static IBiomeProvider Create(
        BiomeSet biomes,
        Func<Vector2I, Vector2I, CardSignature> signatureAt,
        Vector2I mapSize,
        string? defaultPassableTileId = null,
        string? defaultSolidTileId = null)
        => new BiomeMapGenerator(biomes, signatureAt, mapSize, defaultPassableTileId, defaultSolidTileId);
}
