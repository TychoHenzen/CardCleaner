using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.BlackTile;

/// <summary>
///     BlackTileTransitionIntegrityTest scenarios split out of BlackTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlackTileTransitionIntegrityTest : BlackTileReproductionTestBase
{
    // ==================== Null Variant Detection ====================

    [TestCase]
    public void TestIdentifyTransitionsWithNullCriticalVariants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var nullVariants = _transitionMap!.Transitions
            .SelectMany(transition => FindNullCriticalVariants(transition.Key, transition.Value))
            .ToList();

        if (nullVariants.Count > 0)
        {
            GD.PrintErr(
                "BLACK TILE CAUSE: Transitions with null critical variants:\n" +
                $"{string.Join("\n", nullVariants.Take(30))}");
        }

        GD.Print($"Null critical variants found: {nullVariants.Count}");
    }

    private static IEnumerable<string> FindNullCriticalVariants(string key, TransitionEntry entry)
    {
        return entry.Format switch
        {
            "corner16" => FindNullCorner16Variants(key, entry),
            "blob47" => FindNullBlob47Variants(key, entry),
            _ => []
        };
    }

    private static IEnumerable<string> FindNullCorner16Variants(string key, TransitionEntry entry)
    {
        // Variant 15 (all corners = solid fill) is critical
        if (IsNullVariant(entry, 15))
            yield return $"{key}: variant[15] (solid fill) is null";

        // Check common intermediate variants
        for (var i = 1; i < 15; i++)
        {
            if (IsNullVariant(entry, i))
                yield return $"{key}: variant[{i}] is null";
        }
    }

    private static IEnumerable<string> FindNullBlob47Variants(string key, TransitionEntry entry)
    {
        // Variant 0 (isolated) and 46 (solid fill) are critical
        if (IsNullVariant(entry, 0))
            yield return $"{key}: blob47 variant[0] (isolated) is null";
        if (IsNullVariant(entry, 46))
            yield return $"{key}: blob47 variant[46] (solid fill) is null";
    }

    private static bool IsNullVariant(TransitionEntry entry, int index)
    {
        return entry.Variants.Length > index && entry.Variants[index] == null;
    }

    // ==================== Fallback Chain Testing ====================

    [TestCase]
    public void TestResolverFallbackForAllCompositableTiles()
    {
        var fallbackFailures = new List<string>();

        foreach (var tile in _registry.GetAllTiles().Where(t => t.IsCompositable))
        {
            // Test all 16 bitmasks for corner16 format
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                // Test with a common outer terrain (dirt is usually available)
                var result = _resolver.ResolveWithFallback(
                    tile.Id, "dirt", bitmask,
                    99, new Vector2I(-999, -999)); // Invalid fallback to detect failures

                // If result is (0,0), it means ultimate fallback was used
                // If sourceId != 0, something went wrong
                if (result.SourceId != CompiledAtlasSourceId)
                {
                    fallbackFailures.Add($"{tile.Id} bitmask={bitmask}: returned sourceId={result.SourceId}");
                }
                else if (result.AtlasCoords == Vector2I.Zero && bitmask == 15)
                {
                    // Solid fill returning (0,0) might be okay if that's actually the solid fill tile
                    // But it's suspicious - log for investigation
                    fallbackFailures.Add($"{tile.Id} bitmask=15: returned (0,0) - may be fallback");
                }
            }
        }

        if (fallbackFailures.Count > 0)
        {
            GD.PrintErr(
                "POTENTIAL BLACK TILES: Fallback chain issues:\n" +
                $"{string.Join("\n", fallbackFailures.Take(20))}");
        }

        GD.Print($"Fallback chain issues found: {fallbackFailures.Count}");
    }

    // ==================== Terrain Pair Coverage ====================

    [TestCase]
    public void TestCommonTerrainPairsHaveTransitions()
    {
        var commonPairs = new[]
        {
            ("grass", "dirt"),
            ("grass", "sand"),
            ("grass", "water"),
            ("sand", "dirt"),
            ("water", "dirt"),
            ("stone", "dirt"),
        };

        var missingPairs = new List<string>();

        foreach (var (inner, outer) in commonPairs)
        {
            if (!_resolver.HasTransition(inner, outer) && !_resolver.HasTransition($"{inner}_border", outer))
            {
                missingPairs.Add($"{inner}|{outer}");
            }
        }

        if (missingPairs.Count > 0)
        {
            GD.Print($"Common terrain pairs without transitions: {string.Join(", ", missingPairs)}");
        }

        // This is informational - some pairs might not be needed
    }

    // ==================== Self-Transition Detection ====================

    [TestCase]
    public void TestSelfTransitionsExistForCompositableTiles()
    {
        // When terrain borders itself, we need variant 15 (solid fill)
        var missingSelfTransitions = new List<string>();

        foreach (var tile in _registry.GetAllTiles().Where(t => t.IsCompositable))
        {
            var solidFill = _resolver.ResolveSolidFill(tile.Id);
            if (!solidFill.HasValue)
            {
                missingSelfTransitions.Add(tile.Id);
            }
        }

        if (missingSelfTransitions.Count > 0)
        {
            GD.PrintErr(
                "BLACK TILE CAUSE: Compositable tiles without solid fill:\n" +
                $"{string.Join(", ", missingSelfTransitions)}");
        }

        GD.Print($"Compositable tiles without solid fill: {missingSelfTransitions.Count}");
    }

    // ==================== Coordinate Bounds Validation ====================

    [TestCase]
    public void TestAllTransitionCoordsAreNonNegative()
    {
        AssertThat(_transitionMap).IsNotNull();

        var negativeCoords = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null) continue;

                foreach (var variant in variants)
                {
                    if (variant.X < 0 || variant.Y < 0)
                    {
                        negativeCoords.Add($"{key}[{i}]: ({variant.X},{variant.Y})");
                    }
                }
            }
        }

        if (negativeCoords.Count > 0)
        {
            GD.PrintErr(
                "BLACK TILE CAUSE: Negative coordinates in transition_map:\n" +
                $"{string.Join("\n", negativeCoords)}");
        }

        AssertThat(negativeCoords.Count).IsEqual(0);
    }
}
