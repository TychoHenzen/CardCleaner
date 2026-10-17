using CardCleaner.Scripts.Core.Utilities;
using Godot;

namespace CardCleaner.Tests.Core.Utilities;

/// <summary>
///     Pins GodotNoiseSource to the raw FastNoiseLite values it wraps.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GodotNoiseSourceTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void MatchesRawFastNoiseLite()
    {
        int[] seeds = [1, 42];
        float[] frequencies = [0.1f, 0.37f];
        (float X, float Y)[] points = [(0f, 0f), (-5.5f, 12.25f), (100f, 200f)];

        foreach (var seed in seeds)
        foreach (var frequency in frequencies)
        foreach (var (x, y) in points)
        {
            var raw = new FastNoiseLite { Seed = seed, Frequency = frequency };
            AssertFloat(GodotNoiseSource.Create(seed, frequency).GetNoise2D(x, y)).IsEqual(raw.GetNoise2D(x, y));
        }
    }
}
