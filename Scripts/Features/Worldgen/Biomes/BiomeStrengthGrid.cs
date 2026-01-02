using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Pre-computed grid of biome strengths at each position.
/// Computed once before WFC, enabling O(1) lookup during tile selection.
/// </summary>
/// <remarks>
/// Strength values are in the range [-1, 1]:
/// - +1: Position signature is identical to biome's affinity signature
/// - 0: Position signature is at mid-distance from biome
/// - -1: Position signature is maximally distant from biome
/// </remarks>
public class BiomeStrengthGrid
{
    private readonly float[,,] _strengths; // [y, x, biomeIndex]
    private readonly Dictionary<string, int> _biomeToIndex;
    private readonly int _width;
    private readonly int _height;

    // Effective maximum distance for biome differentiation.
    // Biomes typically differ in 1-2 key signature dimensions, not all 8.
    // Using 2*sqrt(2) ≈ 2.83 means: distance 0 = +1, distance ~1.4 = 0, distance 2 = ~-0.4
    // This ensures opposite signatures in one dimension (distance=2) yield negative strength.
    private const float MaxDistance = 2.828427f;

    /// <summary>
    /// Creates a BiomeStrengthGrid by pre-computing strengths for all positions.
    /// </summary>
    /// <param name="mapSize">Size of the map in tiles</param>
    /// <param name="gradient">Gradient providing signatures at each position</param>
    /// <param name="registry">Registry of biomes to compute strengths for</param>
    public BiomeStrengthGrid(Vector2I mapSize, BaselineGradient gradient, BiomeRegistry registry)
    {
        _width = mapSize.X;
        _height = mapSize.Y;

        var biomes = registry.GetAllBiomes().ToList();
        _biomeToIndex = new Dictionary<string, int>();
        for (var i = 0; i < biomes.Count; i++)
        {
            _biomeToIndex[biomes[i].Id] = i;
        }

        _strengths = new float[_height, _width, biomes.Count];
        ComputeStrengths(gradient, biomes, mapSize);
    }

    /// <summary>
    /// Gets the biome strength at a position.
    /// </summary>
    /// <param name="position">Grid position to query</param>
    /// <param name="biomeId">ID of the biome to check</param>
    /// <returns>Strength in [-1, 1] range, or 0 if biome is unknown</returns>
    public float GetStrength(Vector2I position, string biomeId)
    {
        if (!_biomeToIndex.TryGetValue(biomeId, out var index))
            return 0f;

        if (position.X < 0 || position.X >= _width || position.Y < 0 || position.Y >= _height)
            return 0f;

        return _strengths[position.Y, position.X, index];
    }

    /// <summary>
    /// Gets the number of biomes in this grid.
    /// </summary>
    public int BiomeCount => _biomeToIndex.Count;

    /// <summary>
    /// Gets the grid dimensions.
    /// </summary>
    public Vector2I Size => new(_width, _height);

    private void ComputeStrengths(BaselineGradient gradient, List<BiomeDefinition> biomes, Vector2I mapSize)
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var pos = new Vector2I(x, y);
                var signature = gradient.GetSignatureAt(pos, mapSize);

                for (var b = 0; b < biomes.Count; b++)
                {
                    var biome = biomes[b];
                    var strength = CalculateBiomeStrength(signature, biome.AffinitySignature);
                    _strengths[y, x, b] = strength;
                }
            }
        }
    }

    /// <summary>
    /// Calculates biome strength from signature distance.
    /// </summary>
    /// <remarks>
    /// Uses Euclidean distance in 8D signature space.
    /// Converts distance to [-1, 1] range where:
    /// - 0 distance → +1 strength (perfect match)
    /// - MaxDistance → -1 strength (maximally distant)
    /// </remarks>
    private static float CalculateBiomeStrength(CardSignature positionSignature, CardSignature biomeSignature)
    {
        var distance = positionSignature.DistanceTo(biomeSignature);

        // Normalize distance to [0, 1] range
        var normalizedDistance = Mathf.Clamp(distance / MaxDistance, 0f, 1f);

        // Convert to [-1, 1] range: 0 distance → +1, max distance → -1
        return 1f - 2f * normalizedDistance;
    }
}
