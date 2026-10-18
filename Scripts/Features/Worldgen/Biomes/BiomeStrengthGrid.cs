using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Numerics;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Samples biome strengths at every position into an immutable <see cref="BiomeStrengthField"/>.
/// Computed once before WFC, enabling O(1) lookup during tile selection.
/// </summary>
public static class BiomeStrengthGrid
{
    // Effective maximum distance for biome differentiation.
    // Biomes typically differ in 1-2 key signature dimensions, not all 8.
    // Using 2*sqrt(2) ≈ 2.83 means: distance 0 = +1, distance ~1.4 = 0, distance 2 = ~-0.4
    // This ensures opposite signatures in one dimension (distance=2) yield negative strength.
    private const float MaxDistance = 2.828427f;

    /// <summary>
    /// Pre-computes strengths for all positions.
    /// </summary>
    /// <param name="mapSize">Size of the map in tiles</param>
    /// <param name="signatureAt">Gives the signature at each position</param>
    /// <param name="biomes">Biomes to compute strengths for, in their set order</param>
    public static BiomeStrengthField Sample(
        Vector2I mapSize,
        Func<Vector2I, Vector2I, CardSignature> signatureAt,
        BiomeSet biomes)
    {
        var width = mapSize.X;
        var height = mapSize.Y;

        var biomeList = biomes.Biomes;
        var biomeToIndex = new Dictionary<string, int>();
        for (var i = 0; i < biomeList.Count; i++)
        {
            biomeToIndex[biomeList[i].Id] = i;
        }

        var strengths = new float[height, width, biomeList.Count];
        ComputeStrengths(strengths, signatureAt, biomeList, mapSize);

        return new BiomeStrengthField(strengths, biomeToIndex, mapSize);
    }

    private static void ComputeStrengths(
        float[,,] strengths,
        Func<Vector2I, Vector2I, CardSignature> signatureAt,
        IReadOnlyList<BiomeDefinition> biomes,
        Vector2I mapSize)
    {
        var height = mapSize.Y;
        var width = mapSize.X;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pos = new Vector2I(x, y);
                var signature = signatureAt(pos, mapSize);

                for (var b = 0; b < biomes.Count; b++)
                {
                    var biome = biomes[b];
                    var strength = CalculateBiomeStrength(signature, biome.AffinitySignature);
                    strengths[y, x, b] = strength;
                }
            }
        }
    }

    /// <summary>
    /// Maps signature distance to [-1, 1]: 0 is a perfect match (+1), MaxDistance is maximally distant (-1).
    /// </summary>
    private static float CalculateBiomeStrength(CardSignature positionSignature, CardSignature biomeSignature)
    {
        var distance = positionSignature.DistanceTo(biomeSignature);

        var normalizedDistance = CoreMath.Clamp(distance / MaxDistance, 0f, 1f);
        return 1f - 2f * normalizedDistance;
    }
}
