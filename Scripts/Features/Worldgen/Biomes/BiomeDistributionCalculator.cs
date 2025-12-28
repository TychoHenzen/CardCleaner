using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Calculates estimated biome distribution from card signatures.
/// Runs multiple simulations with different random seeds and averages results
/// for more stable predictions.
/// </summary>
public static class BiomeDistributionCalculator
{
    private const int SampleGridSize = 16;
    private const int SimulationCount = 10;

    /// <summary>
    /// Estimates biome distribution for the given card signatures.
    /// Runs multiple simulations and averages results for stability.
    /// </summary>
    /// <param name="cards">The input card signatures (empty returns uniform distribution)</param>
    /// <param name="registry">The biome registry with registered biomes</param>
    /// <returns>Dictionary mapping each biome type to its estimated percentage (0-1)</returns>
    public static Dictionary<BiomeType, float> Calculate(CardSignature[] cards, BiomeRegistry registry)
    {
        var accumulatedCounts = new Dictionary<BiomeType, float>();

        // Initialize counts for all biome types
        foreach (var biome in registry.GetAllBiomes())
        {
            accumulatedCounts[biome.Type] = 0f;
        }

        if (cards.Length == 0 || registry.Count == 0)
        {
            // No cards or no biomes - return uniform distribution
            return ToUniformDistribution(accumulatedCounts);
        }

        // Run multiple simulations and accumulate results
        for (var sim = 0; sim < SimulationCount; sim++)
        {
            var simCounts = RunSingleSimulation(cards, registry);

            foreach (var (biomeType, count) in simCounts)
            {
                accumulatedCounts[biomeType] += count;
            }
        }

        // Average by converting to percentages across all simulations
        var totalSamples = SampleGridSize * SampleGridSize * SimulationCount;
        return ToPercentages(accumulatedCounts, totalSamples);
    }

    /// <summary>
    /// Runs a single simulation with a fresh random seed.
    /// </summary>
    private static Dictionary<BiomeType, int> RunSingleSimulation(CardSignature[] cards, BiomeRegistry registry)
    {
        var counts = new Dictionary<BiomeType, int>();

        // Initialize counts for all biome types
        foreach (var biome in registry.GetAllBiomes())
        {
            counts[biome.Type] = 0;
        }

        // Create gradient with fresh random seed
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        var gradient = new CardBasedGradient(cards, rng);
        var mapSize = new Vector2I(SampleGridSize, SampleGridSize);

        // Sample the grid and count biome occurrences
        for (var y = 0; y < SampleGridSize; y++)
        {
            for (var x = 0; x < SampleGridSize; x++)
            {
                var position = new Vector2I(x, y);
                var signature = gradient.GetSignatureAt(position, mapSize);
                var biome = registry.FindClosestBySignature(signature);

                if (biome != null)
                {
                    counts[biome.Type]++;
                }
            }
        }

        return counts;
    }

    private static Dictionary<BiomeType, float> ToUniformDistribution(Dictionary<BiomeType, float> counts)
    {
        var result = new Dictionary<BiomeType, float>();
        var uniformValue = counts.Count > 0 ? 1f / counts.Count : 0f;

        foreach (var biomeType in counts.Keys)
        {
            result[biomeType] = uniformValue;
        }

        return result;
    }

    private static Dictionary<BiomeType, float> ToPercentages(Dictionary<BiomeType, float> counts, int totalSamples)
    {
        var result = new Dictionary<BiomeType, float>();

        if (totalSamples == 0)
        {
            return ToUniformDistribution(counts);
        }

        foreach (var (biomeType, count) in counts)
        {
            result[biomeType] = count / totalSamples;
        }

        return result;
    }
}
