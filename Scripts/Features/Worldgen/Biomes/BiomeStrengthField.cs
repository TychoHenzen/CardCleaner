using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Immutable grid of biome strengths at each position, produced by <see cref="BiomeStrengthGrid.Sample"/>.
/// Lookups are O(1) during tile selection.
/// </summary>
/// <remarks>
/// Strength values are in the range [-1, 1]:
/// - +1: Position signature is identical to biome's affinity signature
/// - 0: Position signature is at mid-distance from biome
/// - -1: Position signature is maximally distant from biome
/// </remarks>
public sealed class BiomeStrengthField : IBiomeStrengthField
{
    private readonly float[,,] _strengths; // [y, x, biomeIndex]
    private readonly IReadOnlyDictionary<string, int> _biomeToIndex;

    internal BiomeStrengthField(float[,,] strengths, IReadOnlyDictionary<string, int> biomeToIndex, Vector2I size)
    {
        _strengths = strengths;
        _biomeToIndex = biomeToIndex;
        Size = size;
    }

    /// <summary>Gets the number of biomes in this field.</summary>
    public int BiomeCount => _biomeToIndex.Count;

    /// <summary>Gets the field dimensions.</summary>
    public Vector2I Size { get; }

    /// <summary>
    /// Gets the biome strength at a position.
    /// </summary>
    /// <param name="position">Grid position to query</param>
    /// <param name="biomeId">ID of the biome to check</param>
    /// <returns>Strength in [-1, 1] range, or 0 if biome is unknown or position is out of range</returns>
    public float GetStrength(Vector2I position, string biomeId)
    {
        if (!_biomeToIndex.TryGetValue(biomeId, out var index))
            return 0f;

        if (position.X < 0 || position.X >= Size.X || position.Y < 0 || position.Y >= Size.Y)
            return 0f;

        return _strengths[position.Y, position.X, index];
    }
}
