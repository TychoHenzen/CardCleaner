using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Core.Randomness;
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

    /// <summary>Estimates each biome's share of the map for the card signatures, averaged over runs.</summary>
    public static Dictionary<string, float> Calculate(CardSignature[] cards, BiomeSet biomes)
    {
        var accumulatedCounts = new Dictionary<string, float>();

        // Initialize counts for all biome IDs
        foreach (var biome in biomes.Biomes)
        {
            accumulatedCounts[biome.Id] = 0f;
        }

        if (cards.Length == 0 || biomes.Count == 0)
        {
            // No cards or no biomes - return uniform distribution
            return ToUniformDistribution(accumulatedCounts);
        }

        // Run multiple simulations and accumulate results
        for (var sim = 0; sim < SimulationCount; sim++)
        {
            var simCounts = RunSingleSimulation(cards, biomes);

            foreach (var (biomeId, count) in simCounts)
            {
                accumulatedCounts[biomeId] += count;
            }
        }

        // Average by converting to percentages across all simulations
        var totalSamples = SampleGridSize * SampleGridSize * SimulationCount;
        return ToPercentages(accumulatedCounts, totalSamples);
    }

    /// <summary>
    /// Runs a single simulation with a fresh random seed.
    /// </summary>
    private static Dictionary<string, int> RunSingleSimulation(CardSignature[] cards, BiomeSet biomes)
    {
        var counts = new Dictionary<string, int>();

        // Initialize counts for all biome IDs
        foreach (var biome in biomes.Biomes)
        {
            counts[biome.Id] = 0;
        }

        // Create gradient with fresh random seed
        var rng = RandomSources.Create();
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
                var biome = biomes.FindClosestBySignature(signature);

                if (biome != null)
                {
                    counts[biome.Id]++;
                }
            }
        }

        return counts;
    }

    private static Dictionary<string, float> ToUniformDistribution(Dictionary<string, float> counts)
    {
        var result = new Dictionary<string, float>();
        var uniformValue = counts.Count > 0 ? 1f / counts.Count : 0f;

        foreach (var biomeId in counts.Keys)
        {
            result[biomeId] = uniformValue;
        }

        return result;
    }

    private static Dictionary<string, float> ToPercentages(Dictionary<string, float> counts, int totalSamples)
    {
        var result = new Dictionary<string, float>();

        if (totalSamples == 0)
        {
            return ToUniformDistribution(counts);
        }

        foreach (var (biomeId, count) in counts)
        {
            result[biomeId] = count / totalSamples;
        }

        return result;
    }
}
