using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers.Examples;

/// <summary>
/// Example custom modifier that adds noise-based variation to tile weights.
/// Demonstrates how to extend the weight modifier system.
/// </summary>
public sealed class NoiseVariationModifier : IWeightModifier
{
    private readonly FastNoiseLite _noise;
    private readonly Dictionary<string, float> _tileNoisePreferences;

    /// <summary>
    /// How much the noise affects weights (0 = no effect, 1 = full effect).
    /// </summary>
    public float Strength { get; set; } = 0.5f;

    /// <summary>
    /// Create a noise variation modifier.
    /// </summary>
    /// <param name="seed">Noise seed for determinism.</param>
    /// <param name="tileNoisePreferences">
    /// Maps tile IDs to their preferred noise value (0-1).
    /// Tiles get boosted when local noise matches their preference.
    /// </param>
    public NoiseVariationModifier(int seed, Dictionary<string, float> tileNoisePreferences)
    {
        _tileNoisePreferences = tileNoisePreferences;

        _noise = new FastNoiseLite();
        _noise.Seed = seed;
        _noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
        _noise.Frequency = 0.1f;
    }

    public void ApplyModifier(TileSelectionContext context)
    {
        // Get noise value at position (normalized to 0-1)
        var rawNoise = _noise.GetNoise2D(context.Position.X, context.Position.Y);
        var noiseValue = (rawNoise + 1f) / 2f;  // Convert from [-1,1] to [0,1]

        foreach (var tileId in new List<string>(context.Weights.Keys))
        {
            if (!_tileNoisePreferences.TryGetValue(tileId, out var preference))
                continue;

            // Calculate how close the noise is to the tile's preference
            var difference = MathF.Abs(noiseValue - preference);
            var similarity = 1f - difference;  // 1 = perfect match, 0 = opposite

            // Apply strength-scaled multiplier
            // similarity=1 → multiplier=1+Strength, similarity=0 → multiplier=1-Strength
            var multiplier = 1f + (similarity * 2f - 1f) * Strength;
            multiplier = MathF.Max(0.1f, multiplier);  // Floor at 0.1

            context.Weights[tileId] *= multiplier;
        }
    }
}
