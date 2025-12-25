using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Statistical assertions for property-based testing of random distributions.
///     Validates that procedural generation produces statistically fair outputs.
/// </summary>
public static class StatisticalAssertions
{
    public const double DefaultConfidenceLevel = 0.95;

    private static readonly Dictionary<int, ChiSquaredCriticalValues> ChiSquaredTable = new()
    {
        [1] = new ChiSquaredCriticalValues(2.706, 3.841, 6.635),
        [2] = new ChiSquaredCriticalValues(4.605, 5.991, 9.210),
        [3] = new ChiSquaredCriticalValues(6.251, 7.815, 11.345),
        [4] = new ChiSquaredCriticalValues(7.779, 9.488, 13.277),
        [5] = new ChiSquaredCriticalValues(9.236, 11.070, 15.086),
        [6] = new ChiSquaredCriticalValues(10.645, 12.592, 16.812),
        [7] = new ChiSquaredCriticalValues(12.017, 14.067, 18.475),
        [8] = new ChiSquaredCriticalValues(13.362, 15.507, 20.090),
        [9] = new ChiSquaredCriticalValues(14.684, 16.919, 21.666),
        [10] = new ChiSquaredCriticalValues(15.987, 18.307, 23.209),
        [11] = new ChiSquaredCriticalValues(17.275, 19.675, 24.725),
        [12] = new ChiSquaredCriticalValues(18.549, 21.026, 26.217),
        [13] = new ChiSquaredCriticalValues(19.812, 22.362, 27.688),
        [14] = new ChiSquaredCriticalValues(21.064, 23.685, 29.141),
        [15] = new ChiSquaredCriticalValues(22.307, 24.996, 30.578),
        [16] = new ChiSquaredCriticalValues(23.542, 26.296, 32.000),
        [17] = new ChiSquaredCriticalValues(24.769, 27.587, 33.409),
        [18] = new ChiSquaredCriticalValues(25.989, 28.869, 34.805),
        [19] = new ChiSquaredCriticalValues(27.204, 30.144, 36.191),
        [20] = new ChiSquaredCriticalValues(28.412, 31.410, 37.566),
        [21] = new ChiSquaredCriticalValues(29.615, 32.671, 38.932),
        [22] = new ChiSquaredCriticalValues(30.813, 33.924, 40.289),
        [23] = new ChiSquaredCriticalValues(32.007, 35.172, 41.638),
        [24] = new ChiSquaredCriticalValues(33.196, 36.415, 42.980),
        [25] = new ChiSquaredCriticalValues(34.382, 37.652, 44.314),
        [26] = new ChiSquaredCriticalValues(35.563, 38.885, 45.642),
        [27] = new ChiSquaredCriticalValues(36.741, 40.113, 46.963),
        [28] = new ChiSquaredCriticalValues(37.916, 41.337, 48.278),
        [29] = new ChiSquaredCriticalValues(39.087, 42.557, 49.588),
        [30] = new ChiSquaredCriticalValues(40.256, 43.773, 50.892)
    };

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
        var criticalValue = GetChiSquaredCriticalValue(degreesOfFreedom, confidenceLevel);

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
        var criticalValue = GetChiSquaredCriticalValue(degreesOfFreedom, confidenceLevel);

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

    /// <summary>
    ///     Asserts that sample mean is within tolerance of expected value.
    /// </summary>
    /// <param name="samples">The samples to test.</param>
    /// <param name="expectedMean">Expected mean value.</param>
    /// <param name="tolerance">Maximum allowed deviation from expected mean.</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertMean(
        IEnumerable<double> samples,
        double expectedMean,
        double tolerance,
        string? message = null)
    {
        var sampleList = samples.ToList();
        if (sampleList.Count == 0)
        {
            Assertions.AssertThat(false)
                .OverrideFailureMessage("Sample set cannot be empty")
                .IsTrue();
            return;
        }

        var actualMean = sampleList.Average();
        var difference = Math.Abs(actualMean - expectedMean);

        if (difference > tolerance)
        {
            var errorMessage = message ?? "Mean is outside tolerance";
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Expected mean: {expectedMean:F4}\n" +
                $"Actual mean: {actualMean:F4}\n" +
                $"Difference: {difference:F4}\n" +
                $"Tolerance: {tolerance:F4}\n" +
                $"Sample count: {sampleList.Count}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }

    /// <summary>
    ///     Asserts that sample variance is within tolerance of expected value.
    ///     Uses Bessel's correction (n-1 denominator) for unbiased estimation.
    /// </summary>
    /// <param name="samples">The samples to test.</param>
    /// <param name="expectedVariance">Expected variance value.</param>
    /// <param name="tolerance">Maximum allowed deviation from expected variance.</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertVariance(
        IEnumerable<double> samples,
        double expectedVariance,
        double tolerance,
        string? message = null)
    {
        var sampleList = samples.ToList();
        if (sampleList.Count < 2)
        {
            Assertions.AssertThat(false)
                .OverrideFailureMessage("Sample set must have at least 2 values for variance calculation")
                .IsTrue();
            return;
        }

        var mean = sampleList.Average();
        var variance = sampleList.Sum(x => Math.Pow(x - mean, 2)) / (sampleList.Count - 1);
        var difference = Math.Abs(variance - expectedVariance);

        if (difference > tolerance)
        {
            var errorMessage = message ?? "Variance is outside tolerance";
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Expected variance: {expectedVariance:F4}\n" +
                $"Actual variance: {variance:F4}\n" +
                $"Difference: {difference:F4}\n" +
                $"Tolerance: {tolerance:F4}\n" +
                $"Sample count: {sampleList.Count}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }

    /// <summary>
    ///     Asserts that sample standard deviation is within tolerance of expected value.
    /// </summary>
    /// <param name="samples">The samples to test.</param>
    /// <param name="expectedStdDev">Expected standard deviation.</param>
    /// <param name="tolerance">Maximum allowed deviation.</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertStandardDeviation(
        IEnumerable<double> samples,
        double expectedStdDev,
        double tolerance,
        string? message = null)
    {
        var sampleList = samples.ToList();
        if (sampleList.Count < 2)
        {
            Assertions.AssertThat(false)
                .OverrideFailureMessage("Sample set must have at least 2 values for standard deviation calculation")
                .IsTrue();
            return;
        }

        var mean = sampleList.Average();
        var variance = sampleList.Sum(x => Math.Pow(x - mean, 2)) / (sampleList.Count - 1);
        var stdDev = Math.Sqrt(variance);
        var difference = Math.Abs(stdDev - expectedStdDev);

        if (difference > tolerance)
        {
            var errorMessage = message ?? "Standard deviation is outside tolerance";
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Expected std dev: {expectedStdDev:F4}\n" +
                $"Actual std dev: {stdDev:F4}\n" +
                $"Difference: {difference:F4}\n" +
                $"Tolerance: {tolerance:F4}\n" +
                $"Sample count: {sampleList.Count}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }

    /// <summary>
    ///     Asserts that all sample values fall within specified bounds.
    ///     Useful for validating clamped values like CardSignature dimensions.
    /// </summary>
    /// <param name="samples">The samples to test.</param>
    /// <param name="min">Minimum allowed value (inclusive).</param>
    /// <param name="max">Maximum allowed value (inclusive).</param>
    /// <param name="message">Optional custom failure message.</param>
    public static void AssertAllInBounds(
        IEnumerable<double> samples,
        double min,
        double max,
        string? message = null)
    {
        var sampleList = samples.ToList();
        var violations = sampleList.Where(x => x < min || x > max).ToList();

        if (violations.Count > 0)
        {
            var errorMessage = message ?? "Values outside bounds";
            var violationSamples = violations.Take(10).Select(v => $"{v:F4}");
            var violationText = string.Join(", ", violationSamples);
            if (violations.Count > 10) violationText += $" ... and {violations.Count - 10} more";

            Assertions.AssertThat(false).OverrideFailureMessage(
                $"{errorMessage}\n" +
                $"Expected bounds: [{min:F4}, {max:F4}]\n" +
                $"Violations ({violations.Count}): {violationText}\n" +
                $"Sample count: {sampleList.Count}\n" +
                $"{SeedManager.GetReproductionInfo()}").IsTrue();
        }
    }

    private static double GetChiSquaredCriticalValue(int degreesOfFreedom, double confidenceLevel)
    {
        if (degreesOfFreedom < 1 || degreesOfFreedom > 30)
        {
            Assertions.AssertThat(false).OverrideFailureMessage(
                $"Chi-squared critical values not available for {degreesOfFreedom} degrees of freedom. " +
                $"Supported range: 1-30").IsTrue();
            return double.MaxValue;
        }

        var alpha = 1.0 - confidenceLevel;
        var values = ChiSquaredTable[degreesOfFreedom];

        return alpha switch
        {
            0.10 => values.Alpha10,
            0.05 => values.Alpha05,
            0.01 => values.Alpha01,
            _ => throw new ArgumentException(
                $"Unsupported confidence level: {confidenceLevel}. " +
                $"Supported: 0.90 (α=0.10), 0.95 (α=0.05), 0.99 (α=0.01)")
        };
    }

    private readonly record struct ChiSquaredCriticalValues(double Alpha10, double Alpha05, double Alpha01);
}
