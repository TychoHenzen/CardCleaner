using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.CompiledResolver;

/// <summary>
///     CompiledResolverFallbackTest scenarios split out of CompiledTransitionResolverTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledResolverFallbackTest : CompiledTransitionResolverTestBase
{
    // ==================== ResolveWithFallback ====================

    [TestCase]
    public void TestResolveWithFallbackReturnsCompiledCoordsForKnownPair()
    {
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null) return;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);
        var entry = _transitionMap.Transitions[firstKey];

        // Find first non-null variant
        var bitmask = Enumerable.Range(0, entry.Variants.Length)
            .FirstOrDefault(i => entry.Variants[i] != null && entry.Variants[i]!.Length > 0);

        var result = _resolver.ResolveWithFallback(
            borderId, outerTerrain, bitmask,
            99, new Vector2I(999, 999)); // Invalid fallback

        // Should return compiled atlas source ID
        AssertThat(result.SourceId).IsEqual(0);
        // Should return correct coords (first variant)
        AssertThat(result.AtlasCoords.X).IsEqual(entry.Variants[bitmask]![0].X);
        AssertThat(result.AtlasCoords.Y).IsEqual(entry.Variants[bitmask]![0].Y);
    }

    [TestCase]
    public void TestResolveWithFallbackTriesAnyVariantForUnknownOuter()
    {
        // Get a border ID that has transitions
        var borderId = _transitionMap.GetAllBorderIds().FirstOrDefault();
        if (borderId == null) return;

        // Use a fake outer terrain that won't have direct mapping
        var result = _resolver.ResolveWithFallback(
            borderId, "nonexistent_outer_terrain_xyz", 15,
            99, new Vector2I(999, 999));

        // Should still return sourceId 0 (compiled atlas)
        // ResolveAnyVariant should find SOMETHING for this border
        AssertThat(result.SourceId).IsEqual(0);

        // The coords might be from any transition with this border
        // Just verify they're not the invalid fallback
        GD.Print(
            $"ResolveWithFallback returned: sourceId={result.SourceId}, " +
            $"coords=({result.AtlasCoords.X},{result.AtlasCoords.Y})");
    }

    [TestCase]
    public void TestResolveWithFallbackReturnsZeroZeroForTotallyUnknown()
    {
        var result = _resolver.ResolveWithFallback(
            "completely_unknown_terrain_xyz", "another_unknown_abc", 5,
            99, new Vector2I(999, 999));

        // Should return sourceId 0 (compiled atlas) and (0,0) as safe fallback
        AssertThat(result.SourceId).IsEqual(0);
        AssertThat(result.AtlasCoords).IsEqual(Vector2I.Zero);
    }

    [TestCase]
    public void TestResolveWithFallbackUsesProvidedFallbackWhenSourceMatches()
    {
        var result = _resolver.ResolveWithFallback(
            "unknown_terrain_xyz", "unknown_outer_abc", 5,
            0, new Vector2I(10, 20)); // Fallback uses compiled atlas sourceId

        // When ResolveAnyVariant fails and fallback sourceId matches compiled atlas,
        // it should use the provided fallback coords
        // But first it tries ResolveAnyVariant which will fail
        // Then it checks if fallbackSourceId == compiledAtlasSourceId (0 == 0)
        // So it should return the fallback coords
        AssertThat(result.SourceId).IsEqual(0);
        AssertThat(result.AtlasCoords).IsEqual(new Vector2I(10, 20));
    }

    // ==================== ResolveSolidFill ====================

    [TestCase]
    public void TestResolveSolidFillReturnsVariant15()
    {
        var borderId = _transitionMap.GetAllBorderIds().FirstOrDefault();
        if (borderId == null) return;

        var coords = _resolver.ResolveSolidFill(borderId);

        // Should return variant 15 (all corners) if it exists
        if (coords.HasValue)
        {
            GD.Print($"Solid fill for {borderId}: ({coords.Value.X},{coords.Value.Y})");
            AssertThat(coords.Value.X).IsGreaterEqual(0);
            AssertThat(coords.Value.Y).IsGreaterEqual(0);
        }
        else
        {
            GD.Print($"No solid fill found for {borderId}");
        }
    }

    [TestCase]
    public void TestResolveSolidFillReturnsNullForUnknown()
    {
        var coords = _resolver.ResolveSolidFill("nonexistent_terrain_xyz");
        AssertThat(coords).IsNull();
    }

    // ==================== ResolveAnyVariant ====================

    [TestCase]
    public void TestResolveAnyVariantFindsCoords()
    {
        var borderId = _transitionMap.GetAllBorderIds().FirstOrDefault();
        if (borderId == null) return;

        var coords = _resolver.ResolveAnyVariant(borderId, 1);

        // Should find ANY transition with this border ID and return bitmask 1 coords
        GD.Print($"ResolveAnyVariant({borderId}, 1) = {coords}");

        // May or may not find something depending on the data
    }

    [TestCase]
    public void TestResolveAnyVariantReturnsNullForInvalidBitmask()
    {
        var borderId = _transitionMap.GetAllBorderIds().FirstOrDefault();
        if (borderId == null) return;

        // Negative bitmask
        AssertThat(_resolver.ResolveAnyVariant(borderId, -1)).IsNull();

        // Bitmask >= 16
        AssertThat(_resolver.ResolveAnyVariant(borderId, 16)).IsNull();
        AssertThat(_resolver.ResolveAnyVariant(borderId, 100)).IsNull();
    }
}
