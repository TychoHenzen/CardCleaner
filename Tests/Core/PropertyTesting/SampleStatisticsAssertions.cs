using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Statistical assertions on sample moments and bounds for property-based testing of random values.
/// </summary>
public static class SampleStatisticsAssertions
{
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
}
