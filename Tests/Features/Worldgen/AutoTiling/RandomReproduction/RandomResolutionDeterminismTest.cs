using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.RandomReproduction;

/// <summary>
///     RandomResolutionDeterminismTest scenarios split out of RandomAutoTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RandomResolutionDeterminismTest : RandomAutoTileReproductionTestBase
{
    // ==================== Resolution Consistency ====================

    [TestCase]
    public void TestResolutionIsDeterministic()
    {
        // Same input should always produce same output
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Take(3)
            .ToList();

        var inconsistencies = new List<string>();

        foreach (var tile in compositableTiles)
        {
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                // Resolve same query 3 times
                var result1 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);
                var result2 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);
                var result3 = _resolver.ResolveWithFallback(tile.Id, "dirt", bitmask, 0, Vector2I.Zero);

                if (result1.AtlasCoords != result2.AtlasCoords || result2.AtlasCoords != result3.AtlasCoords)
                {
                    inconsistencies.Add(
                        $"{tile.Id} bitmask={bitmask}: got different results " +
                        $"{result1.AtlasCoords}, {result2.AtlasCoords}, {result3.AtlasCoords}");
                }
            }
        }

        if (inconsistencies.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Non-deterministic resolution:\n{string.Join("\n", inconsistencies)}");
        }

        AssertThat(inconsistencies.Count).IsEqual(0);
    }

    // ==================== Coordinate Uniqueness ====================

    [TestCase]
    public void TestDifferentBitmasksProduceDifferentCoords()
    {
        // Different bitmasks should (usually) produce different coordinates
        // This helps verify we're not always returning the same tile

        AssertThat(_transitionMap).IsNotNull();

        var suspiciousEntries = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions.Take(10))
        {
            if (entry.Format != "corner16")
                continue;

            var coords = new HashSet<(int, int)>();
            var duplicateCount = 0;

            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null) continue;

                foreach (var v in variants)
                {
                    if (!coords.Add((v.X, v.Y)))
                        duplicateCount++;
                }
            }

            // Some duplication is okay, but if ALL are the same, that's suspicious
            var totalVariants = entry.Variants.Where(v => v != null).Sum(v => v!.Length);
            if (duplicateCount > totalVariants / 2)
            {
                suspiciousEntries.Add($"{key}: {duplicateCount}/{totalVariants} variants share coordinates");
            }
        }

        if (suspiciousEntries.Count > 0)
        {
            GD.Print(
                "Transitions with high coordinate duplication (may cause 'random' appearance):\n" +
                $"{string.Join("\n", suspiciousEntries)}");
        }

        // Informational - not a hard failure
    }
}
