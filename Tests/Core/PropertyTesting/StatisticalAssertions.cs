using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Statistical assertions for property-based testing of random distributions.
///     Validates that procedural generation produces statistically fair outputs.
/// </summary>
public static class StatisticalAssertions
{
    public const double DefaultConfidenceLevel = 0.95;

    /// <summary>
    ///     Asserts that samples follow a uniform distribution using chi-squared test.
    /// </summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="samples">The samples to test.</param>
    /// <param name="confidenceLevel">Confidence level (0.90, 0.95, or 0.99). Default is 0.95.</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertUniformDistribution<T>(
        IEnumerable<T> samples,
        double confidenceLevel = DefaultConfidenceLevel,
        string? message = null) where T : notnull
    {
        var sampleList = samples.ToList();
        if (sampleList.Count == 0)
        {
            Assertions.AssertThat(false)
                .OverrideFailureMessage("Sample set cannot be empty")
                .IsTrue();
            return;
        }

        var counts = sampleList
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        var categories = counts.Count;
        var totalSamples = sampleList.Count;
        var expectedFrequency = (double)totalSamples / categories;

        var chiSquared = counts.Values
            .Sum(observed => Math.Pow(observed - expectedFrequency, 2) / expectedFrequency);

        var degreesOfFreedom = categories - 1;
        var criticalValue = ChiSquaredCriticalTable.Lookup(degreesOfFreedom, confidenceLevel);

        if (chiSquared > criticalValue)
        {
            var observed = string.Join(", ",
                counts.OrderBy(kvp => kvp.Key.ToString()).Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var errorMessage = message ?? "Distribution is not uniform";
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Chi-squared: {chiSquared:F2} (critical: {criticalValue:F2})\n" +
                $"Degrees of freedom: {degreesOfFreedom}\n" +
                $"Confidence: {confidenceLevel * 100}%\n" +
                $"Expected frequency: {expectedFrequency:F2} per category\n" +
                $"Observed: {observed}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }

    /// <summary>
    ///     Asserts that samples match expected weighted distribution using chi-squared test.
    /// </summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="samples">The samples to test.</param>
    /// <param name="expectedWeights">Dictionary mapping categories to their expected weights.</param>
    /// <param name="confidenceLevel">Confidence level (0.90, 0.95, or 0.99). Default is 0.95.</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertWeightedDistribution<T>(
        IEnumerable<T> samples,
        Dictionary<T, double> expectedWeights,
        double confidenceLevel = DefaultConfidenceLevel,
        string? message = null) where T : notnull
    {
        var sampleList = samples.ToList();
        if (sampleList.Count == 0)
        {
            Assertions.AssertThat(false)
                .OverrideFailureMessage("Sample set cannot be empty")
                .IsTrue();
            return;
        }

        var totalWeight = expectedWeights.Values.Sum();
        var counts = sampleList
            .GroupBy(x => x)
            .ToDictionary(g => g.Key, g => g.Count());

        var totalSamples = sampleList.Count;
        var chiSquared = 0.0;

        foreach (var (category, weight) in expectedWeights)
        {
            var expectedFrequency = weight / totalWeight * totalSamples;
            var observed = counts.GetValueOrDefault(category, 0);
            if (expectedFrequency > 0) chiSquared += Math.Pow(observed - expectedFrequency, 2) / expectedFrequency;
        }

        var degreesOfFreedom = expectedWeights.Count - 1;
        var criticalValue = ChiSquaredCriticalTable.Lookup(degreesOfFreedom, confidenceLevel);

        if (chiSquared > criticalValue)
        {
            var observed = string.Join(", ",
                counts.OrderBy(kvp => kvp.Key?.ToString()).Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var expected = string.Join(", ",
                expectedWeights.OrderBy(kvp => kvp.Key?.ToString()).Select(kvp => $"{kvp.Key}={kvp.Value:F1}"));
            var errorMessage = message ?? "Distribution does not match expected weights";
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Chi-squared: {chiSquared:F2} (critical: {criticalValue:F2})\n" +
                $"Degrees of freedom: {degreesOfFreedom}\n" +
                $"Confidence: {confidenceLevel * 100}%\n" +
                $"Expected weights: {expected}\n" +
                $"Observed counts: {observed}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }
}
