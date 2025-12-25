using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting;

/// <summary>
///     Utility for managing random seeds in property-based tests.
///     Captures seeds for reproducibility and enables replay of failed test cases.
/// </summary>
public static class SeedManager
{
    /// <summary>
    ///     Gets the last captured seed value.
    /// </summary>
    public static ulong LastSeed { get; private set; }

    /// <summary>
    ///     Whether a seed has been captured in the current test run.
    /// </summary>
    public static bool HasCapturedSeed { get; private set; }

    /// <summary>
    ///     Resets the seed manager state. Call this in test setup.
    /// </summary>
    public static void Reset()
    {
        LastSeed = 0;
        HasCapturedSeed = false;
    }

    /// <summary>
    ///     Creates a new RandomNumberGenerator with a captured seed.
    ///     If no explicit seed is provided, generates one from system time.
    ///     The seed is stored for later reproduction.
    /// </summary>
    /// <param name="explicitSeed">Optional explicit seed for deterministic replay.</param>
    /// <returns>A seeded RandomNumberGenerator instance.</returns>
    public static RandomNumberGenerator CreateRng(ulong? explicitSeed = null)
    {
        var rng = new RandomNumberGenerator();
        var seed = explicitSeed ?? GenerateSeed();

        rng.Seed = seed;
        LastSeed = seed;
        HasCapturedSeed = true;

        return rng;
    }

    /// <summary>
    ///     Applies a captured seed to an existing RandomNumberGenerator.
    ///     Useful when you need to reset an RNG to replay a sequence.
    /// </summary>
    /// <param name="rng">The RNG to seed.</param>
    /// <param name="explicitSeed">Optional explicit seed. Uses last captured seed if not provided.</param>
    public static void ApplySeed(RandomNumberGenerator rng, ulong? explicitSeed = null)
    {
        var seed = explicitSeed ?? (HasCapturedSeed ? LastSeed : GenerateSeed());

        rng.Seed = seed;
        LastSeed = seed;
        HasCapturedSeed = true;
    }

    /// <summary>
    ///     Formats seed information for inclusion in test failure messages.
    /// </summary>
    /// <returns>A string describing how to reproduce with the captured seed.</returns>
    public static string GetReproductionInfo()
    {
        if (!HasCapturedSeed) return "No seed captured. Call CreateRng() or ApplySeed() to capture a seed.";

        return $"To reproduce, use seed: {LastSeed}UL";
    }

    /// <summary>
    ///     Generates a seed value from the current system time.
    /// </summary>
    private static ulong GenerateSeed() => Time.GetTicksUsec();
}
