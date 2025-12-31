using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Identifies and reproduces conditions that cause black/invalid tile rendering.
/// Black tiles occur when:
/// - Tile coordinates reference unmapped source IDs
/// - Coordinates fall outside atlas bounds
/// - Transition lookups fail without valid fallback
/// - Bitmask values produce null variants
///
/// These tests systematically check for these conditions to identify the ~10% failure rate.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BlackTileReproductionTest
{
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";
    private const string AtlasMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";
    private const int CompiledAtlasSourceId = 0;

    private TileRegistry _registry = null!;
    private CompiledTransitionResolver _resolver = null!;
    private CompiledAtlasLoader.AtlasMappingData? _atlasMapping;
    private CompiledTransitionMap? _transitionMap;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();
        _atlasMapping = CompiledAtlasLoader.LoadMapping();

        var transitionPath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (File.Exists(transitionPath))
        {
            var json = File.ReadAllText(transitionPath);
            _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }

    // ==================== Unmapped Source ID Detection ====================

    [TestCase]
    public void TestIdentifyTilesWithUnmappedSourceIds()
    {
        AssertThat(_atlasMapping).IsNotNull();

        var unmappedTiles = new List<string>();
        var mappedSourceIds = _atlasMapping!.Sources!.Keys.Select(int.Parse).ToHashSet();

        foreach (var tile in _registry.GetAllTiles())
        {
            // Non-compositable tiles should have their base coords in atlas_mapping
            if (!tile.IsCompositable && !mappedSourceIds.Contains(tile.SourceId))
            {
                unmappedTiles.Add($"{tile.Id}: sourceId={tile.SourceId} not in atlas_mapping");
            }
        }

        if (unmappedTiles.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Tiles with unmapped source IDs:\n{string.Join("\n", unmappedTiles)}");
        }

        // Log count for diagnosis
        GD.Print($"Tiles with unmapped source IDs: {unmappedTiles.Count}");
    }

    [TestCase]
    public void TestIdentifyTilesWithUnmappedCoordinates()
    {
        AssertThat(_atlasMapping).IsNotNull();

        var unmappedCoords = new List<string>();

        foreach (var tile in _registry.GetAllTiles())
        {
            if (tile.IsCompositable)
                continue; // Compositable tiles use transition_map

            var sourceKey = tile.SourceId.ToString();
            var coordKey = $"{tile.AtlasCoords.X},{tile.AtlasCoords.Y}";

            if (!_atlasMapping!.Sources!.TryGetValue(sourceKey, out var coordMappings) ||
                !coordMappings.ContainsKey(coordKey))
            {
                // Check if using compiled atlas sourceId (coords already translated)
                if (tile.SourceId != CompiledAtlasSourceId)
                {
                    unmappedCoords.Add($"{tile.Id}: ({tile.AtlasCoords.X},{tile.AtlasCoords.Y}) from source {tile.SourceId}");
                }
            }
        }

        if (unmappedCoords.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Tiles with unmapped coordinates:\n{string.Join("\n", unmappedCoords.Take(20))}...");
        }

        GD.Print($"Tiles with unmapped coordinates: {unmappedCoords.Count}");
    }

    // ==================== Missing Transition Detection ====================

    [TestCase]
    public void TestIdentifyCompositableTilesWithoutTransitions()
    {
        AssertThat(_transitionMap).IsNotNull();

        var missingTransitions = new List<string>();
        var allBorderIds = _transitionMap!.GetAllBorderIds().ToHashSet();

        foreach (var tile in _registry.GetAllTiles())
        {
            if (!tile.IsCompositable)
                continue;

            // Check if this tile has ANY transition entry
            if (!allBorderIds.Contains(tile.Id) && !allBorderIds.Contains($"{tile.Id}_border"))
            {
                missingTransitions.Add(tile.Id);
            }
        }

        if (missingTransitions.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Compositable tiles without transitions:\n{string.Join(", ", missingTransitions)}");
        }

        AssertThat(missingTransitions.Count).IsEqual(0);
    }

    // ==================== Null Variant Detection ====================

    [TestCase]
    public void TestIdentifyTransitionsWithNullCriticalVariants()
    {
        AssertThat(_transitionMap).IsNotNull();

        var nullVariants = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format == "corner16")
            {
                // Variant 15 (all corners = solid fill) is critical
                if (entry.Variants.Length > 15 && entry.Variants[15] == null)
                    nullVariants.Add($"{key}: variant[15] (solid fill) is null");

                // Check common intermediate variants
                for (var i = 1; i < 15; i++)
                {
                    if (entry.Variants.Length > i && entry.Variants[i] == null)
                        nullVariants.Add($"{key}: variant[{i}] is null");
                }
            }
            else if (entry.Format == "blob47")
            {
                // Variant 0 (isolated) and 46 (solid fill) are critical
                if (entry.Variants.Length > 0 && entry.Variants[0] == null)
                    nullVariants.Add($"{key}: blob47 variant[0] (isolated) is null");
                if (entry.Variants.Length > 46 && entry.Variants[46] == null)
                    nullVariants.Add($"{key}: blob47 variant[46] (solid fill) is null");
            }
        }

        if (nullVariants.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Transitions with null critical variants:\n{string.Join("\n", nullVariants.Take(30))}");
        }

        GD.Print($"Null critical variants found: {nullVariants.Count}");
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
            GD.PrintErr($"POTENTIAL BLACK TILES: Fallback chain issues:\n{string.Join("\n", fallbackFailures.Take(20))}");
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
            GD.PrintErr($"BLACK TILE CAUSE: Compositable tiles without solid fill:\n{string.Join(", ", missingSelfTransitions)}");
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
                var variant = entry.Variants[i];
                if (variant == null) continue;

                if (variant.X < 0 || variant.Y < 0)
                {
                    negativeCoords.Add($"{key}[{i}]: ({variant.X},{variant.Y})");
                }
            }
        }

        if (negativeCoords.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CAUSE: Negative coordinates in transition_map:\n{string.Join("\n", negativeCoords)}");
        }

        AssertThat(negativeCoords.Count).IsEqual(0);
    }

    // ==================== Black Tile Simulation ====================

    [TestCase]
    public void TestSimulateUniformTerrainRendering()
    {
        // Simulate rendering a 5x5 area of uniform grass terrain
        // This should produce all bitmask 15 (solid fill) tiles

        var blackTileConditions = new List<string>();

        foreach (var tile in _registry.GetAllTiles().Where(t => t.IsCompositable))
        {
            // For uniform terrain, we'd render at bitmask 15 (all corners filled)
            var coords = _resolver.ResolveTransition(tile.Id, tile.Id, 15);
            if (!coords.HasValue)
            {
                // Try fallback
                coords = _resolver.ResolveAnyVariant(tile.Id, 15);
                if (!coords.HasValue)
                {
                    blackTileConditions.Add($"Uniform {tile.Id}: no valid coords for bitmask 15");
                }
            }
        }

        if (blackTileConditions.Count > 0)
        {
            GD.PrintErr($"BLACK TILE CONDITIONS (uniform terrain):\n{string.Join("\n", blackTileConditions)}");
        }

        GD.Print($"Uniform terrain black tile conditions: {blackTileConditions.Count}");
    }

    [TestCase]
    public void TestSimulateBoundaryTerrainRendering()
    {
        // Simulate rendering terrain boundaries
        // Get pairs of compositable tiles

        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var blackTileConditions = new List<string>();

        // Test a few boundary bitmasks
        var boundaryBitmasks = new[] { 1, 2, 3, 4, 5, 8, 9, 10, 12, 14 };

        foreach (var inner in compositableTiles.Take(5)) // Limit for test speed
        {
            foreach (var outer in compositableTiles.Take(5).Where(o => o.Id != inner.Id))
            {
                foreach (var bitmask in boundaryBitmasks)
                {
                    var coords = _resolver.ResolveTransition(inner.Id, outer.Id, bitmask);
                    if (!coords.HasValue)
                    {
                        // Check if ANY variant exists
                        var anyVariant = _resolver.ResolveAnyVariant(inner.Id, bitmask);
                        if (!anyVariant.HasValue)
                        {
                            blackTileConditions.Add($"{inner.Id}|{outer.Id} bitmask={bitmask}: no coords");
                        }
                    }
                }
            }
        }

        if (blackTileConditions.Count > 0)
        {
            GD.Print($"Boundary rendering potential black tiles:\n{string.Join("\n", blackTileConditions.Take(20))}...");
        }

        GD.Print($"Boundary terrain black tile conditions: {blackTileConditions.Count}");
    }

    // ==================== Summary Statistics ====================

    [TestCase]
    public void TestBlackTileRiskSummary()
    {
        var totalTiles = _registry.GetAllTiles().Count();
        var compositableTiles = _registry.GetAllTiles().Count(t => t.IsCompositable);
        var totalTransitions = _transitionMap?.Transitions.Count ?? 0;

        var nullVariantCount = _transitionMap?.Transitions.Values
            .Sum(e => e.Variants.Count(v => v == null)) ?? 0;

        var totalVariantSlots = _transitionMap?.Transitions.Values
            .Sum(e => e.Variants.Length) ?? 0;

        var nullPercentage = totalVariantSlots > 0
            ? (nullVariantCount * 100.0 / totalVariantSlots)
            : 0;

        GD.Print("=== BLACK TILE RISK SUMMARY ===");
        GD.Print($"Total tiles in registry: {totalTiles}");
        GD.Print($"Compositable tiles: {compositableTiles}");
        GD.Print($"Total transitions in map: {totalTransitions}");
        GD.Print($"Null variant slots: {nullVariantCount}/{totalVariantSlots} ({nullPercentage:F1}%)");

        // The null percentage gives us an estimate of black tile risk
        // If close to 10%, this may explain the user's observation
        GD.Print($"\n>>> Estimated black tile risk from null variants: {nullPercentage:F1}%");

        AssertThat(totalTransitions).IsGreater(0);
    }
}
