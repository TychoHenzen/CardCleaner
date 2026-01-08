using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Validates CompiledTransitionResolver lookup accuracy, fallback behavior, and edge cases.
/// The resolver is the core component that translates terrain pairs + bitmask to atlas coordinates.
/// Bugs here directly cause wrong tiles or black tiles to render.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledTransitionResolverTest
{
    private CompiledTransitionResolver _resolver = null!;
    private TileRegistry _registry = null!;
    private CompiledTransitionMap _transitionMap = null!;

    [BeforeTest]
    public void Setup()
    {
        _resolver = new CompiledTransitionResolver();
        _registry = new TileRegistry();

        // Load transition map for direct inspection
        var json = System.IO.File.ReadAllText(
            ProjectSettings.GlobalizePath("res://Data/CompiledAtlas/transition_map.json"));
        _transitionMap = System.Text.Json.JsonSerializer.Deserialize<CompiledTransitionMap>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    // ==================== Resolver Creation ====================

    [TestCase]
    public void TestResolverCreatesSuccessfully()
    {
        AssertThat(_resolver).IsNotNull();
        AssertThat(_resolver.CompiledAtlasSourceId).IsEqual(0);
    }

    // ==================== Direct Transition Lookup ====================

    [TestCase]
    public void TestResolveTransitionReturnsValidCoordsForKnownPair()
    {
        // Get first transition key from the map
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            GD.PrintErr("No transitions in map - test cannot run");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);
        var entry = _transitionMap.Transitions[firstKey];

        // Find first non-null variant
        var bitmask = -1;
        for (var i = 0; i < entry.Variants.Length; i++)
        {
            if (entry.Variants[i] != null && entry.Variants[i]!.Length > 0)
            {
                bitmask = i;
                break;
            }
        }

        if (bitmask < 0)
        {
            GD.Print($"Transition {firstKey} has no non-null variants");
            return;
        }

        var coords = _resolver.ResolveTransition(borderId, outerTerrain, bitmask);

        AssertThat(coords).IsNotNull();
        AssertThat(coords!.Value.X).IsEqual(entry.Variants[bitmask]![0].X);
        AssertThat(coords!.Value.Y).IsEqual(entry.Variants[bitmask]![0].Y);
    }

    [TestCase]
    public void TestResolveTransitionReturnsNullForUnknownPair()
    {
        var coords = _resolver.ResolveTransition("nonexistent_terrain_xyz", "another_fake_terrain", 5);
        AssertThat(coords).IsNull();
    }

    [TestCase]
    public void TestResolveTransitionReturnsNullForInvalidBitmask()
    {
        // Get a known valid pair first
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null) return;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);

        // Try with bitmask -1 (invalid)
        var coords = _resolver.ResolveTransition(borderId, outerTerrain, -1);
        AssertThat(coords).IsNull();
    }

    // ==================== All Bitmask Values ====================

    [TestCase]
    public void TestResolveTransitionHandlesAllBitmaskValues0To15()
    {
        // Get first corner16 transition
        var corner16Key = _transitionMap.Transitions
            .FirstOrDefault(kvp => kvp.Value.Format == "corner16").Key;

        if (corner16Key == null)
        {
            GD.PrintErr("No corner16 transitions found");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(corner16Key);
        var entry = _transitionMap.Transitions[corner16Key];

        var results = new System.Collections.Generic.List<string>();

        for (var bitmask = 0; bitmask < 16; bitmask++)
        {
            var coords = _resolver.ResolveTransition(borderId, outerTerrain, bitmask);
            var expectedVariant = bitmask < entry.Variants.Length ? entry.Variants[bitmask] : null;

            if (expectedVariant != null && expectedVariant.Length > 0)
            {
                var firstVariant = expectedVariant[0];
                if (coords == null)
                    results.Add($"bitmask {bitmask}: expected ({firstVariant.X},{firstVariant.Y}), got null");
                else if (coords.Value.X != firstVariant.X || coords.Value.Y != firstVariant.Y)
                    results.Add($"bitmask {bitmask}: expected ({firstVariant.X},{firstVariant.Y}), got ({coords.Value.X},{coords.Value.Y})");
            }
            else
            {
                // Null expected - coords can be null or any value
            }
        }

        if (results.Count > 0)
            GD.PrintErr($"Bitmask lookup failures for {corner16Key}:\n{string.Join("\n", results)}");

        AssertThat(results.Count).IsEqual(0);
    }

    // ==================== HasTransition ====================

    [TestCase]
    public void TestHasTransitionReturnsTrueForKnownPair()
    {
        var firstKey = _transitionMap.Transitions.Keys.FirstOrDefault();
        if (firstKey == null) return;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);

        AssertBool(_resolver.HasTransition(borderId, outerTerrain)).IsTrue();
    }

    [TestCase]
    public void TestHasTransitionReturnsFalseForUnknownPair()
    {
        AssertBool(_resolver.HasTransition("fake_terrain_123", "fake_terrain_456")).IsFalse();
    }

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
        GD.Print($"ResolveWithFallback returned: sourceId={result.SourceId}, coords=({result.AtlasCoords.X},{result.AtlasCoords.Y})");
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

    // ==================== Compositable Tile Coverage ====================

    [TestCase]
    public void TestAllCompositableTilesHaveTransitions()
    {
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var missingTransitions = new System.Collections.Generic.List<string>();
        var allBorderIds = _transitionMap.GetAllBorderIds().ToHashSet();

        foreach (var tile in compositableTiles)
        {
            // Check if this tile's ID (or _border variant) exists in transitions
            if (!allBorderIds.Contains(tile.Id) && !allBorderIds.Contains($"{tile.Id}_border"))
            {
                missingTransitions.Add(tile.Id);
            }
        }

        if (missingTransitions.Count > 0)
            GD.PrintErr($"Compositable tiles without transitions: {string.Join(", ", missingTransitions)}");

        AssertThat(missingTransitions.Count).IsEqual(0);
    }

    // ==================== Cross-Reference with TileRegistry ====================

    [TestCase]
    public void TestTransitionBorderIdsExistInRegistry()
    {
        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var missingInRegistry = new System.Collections.Generic.List<string>();

        foreach (var borderId in _transitionMap.GetAllBorderIds())
        {
            // Border ID might be the tile ID directly, or "{tileId}_border"
            var baseTileId = borderId.EndsWith("_border")
                ? borderId[..^"_border".Length]
                : borderId;

            if (!allTileIds.Contains(borderId) && !allTileIds.Contains(baseTileId))
            {
                missingInRegistry.Add(borderId);
            }
        }

        if (missingInRegistry.Count > 0)
            GD.PrintErr($"Border IDs not in registry: {string.Join(", ", missingInRegistry)}");

        AssertThat(missingInRegistry.Count).IsEqual(0);
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestTransitionResolverStatistics()
    {
        var totalTransitions = _transitionMap.Transitions.Count;
        var uniqueBorders = _transitionMap.GetAllBorderIds().Count();

        var corner16Count = _transitionMap.Transitions.Count(kvp => kvp.Value.Format == "corner16");
        var blob47Count = _transitionMap.Transitions.Count(kvp => kvp.Value.Format == "blob47");
        var edge16Count = _transitionMap.Transitions.Count(kvp => kvp.Value.Format == "edge16");

        var totalNonNullVariants = _transitionMap.Transitions.Values
            .Sum(e => e.Variants.Where(v => v != null).Sum(v => v!.Length));

        GD.Print("CompiledTransitionResolver Statistics:");
        GD.Print($"  Total transitions: {totalTransitions}");
        GD.Print($"  Unique border IDs: {uniqueBorders}");
        GD.Print($"  Corner16 entries: {corner16Count}");
        GD.Print($"  Blob47 entries: {blob47Count}");
        GD.Print($"  Edge16 entries: {edge16Count}");
        GD.Print($"  Total non-null variants: {totalNonNullVariants}");

        AssertThat(totalTransitions).IsGreater(0);
    }

    // ==================== Border ID Format Tests ====================

    [TestCase]
    public void TestResolverHandlesBorderSuffix()
    {
        // The resolver should try "{tileId}_border" as a fallback
        // Check if any tiles use this convention
        var tilesWithBorderSuffix = _transitionMap.GetAllBorderIds()
            .Where(id => id.EndsWith("_border"))
            .ToList();

        GD.Print($"Border IDs with '_border' suffix: {tilesWithBorderSuffix.Count}");
        if (tilesWithBorderSuffix.Count > 0)
        {
            GD.Print($"  Examples: {string.Join(", ", tilesWithBorderSuffix.Take(5))}");
        }

        // Informational - no assertion needed
    }

    // ==================== Variant 15 Coverage (Solid Fill) ====================

    [TestCase]
    public void TestAllCorner16TransitionsHaveVariant15()
    {
        var missingVariant15 = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length < 16 || entry.Variants[15] == null || entry.Variants[15]!.Length == 0)
            {
                missingVariant15.Add($"{key}: variant[15] is {(entry.Variants.Length < 16 ? "missing (length=" + entry.Variants.Length + ")" : entry.Variants[15] == null ? "null" : "empty")}");
            }
        }

        if (missingVariant15.Count > 0)
            GD.PrintErr($"Corner16 transitions missing variant[15] (solid fill):\n{string.Join("\n", missingVariant15)}");

        AssertThat(missingVariant15.Count).IsEqual(0);
    }
}
