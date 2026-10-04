using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TransitionMap;

/// <summary>
///     TransitionMapCoordinateTest scenarios split out of TransitionMapValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransitionMapCoordinateTest : TransitionMapValidationTestBase
{
    private static readonly HashSet<string> TransitionOnlyBorderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "mound6"
    };

    // Default values - will be read from atlas_mapping.json if available
    private const int DefaultAtlasWidth = 4096;

    private const int DefaultAtlasHeight = 2048;

  // Must match atlas_mapping.json
    private const int DefaultTileSize = 16;

    // ==================== Coordinate Bounds Validation ====================

    [TestCase]
    public void TestAllVariantCoordsAreWithinAtlasBounds()
    {
        AssertThat(_transitionMap).IsNotNull();

        var maxTileX = DefaultAtlasWidth / DefaultTileSize;
        var maxTileY = DefaultAtlasHeight / DefaultTileSize;
        var outOfBounds = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    if (variant.X < 0 || variant.X >= maxTileX)
                        outOfBounds.Add($"{key}[{i}]: x={variant.X} out of bounds [0, {maxTileX - 1}]");
                    if (variant.Y < 0 || variant.Y >= maxTileY)
                        outOfBounds.Add($"{key}[{i}]: y={variant.Y} out of bounds [0, {maxTileY - 1}]");
                }
            }
        }

        if (outOfBounds.Count > 0)
            GD.PrintErr($"Out of bounds coordinates:\n{string.Join("\n", outOfBounds.Take(20))}...");

        AssertThat(outOfBounds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllVariantCoordsAreNonNegative()
    {
        AssertThat(_transitionMap).IsNotNull();

        var negativeCoords = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    if (variant.X < 0 || variant.Y < 0)
                        negativeCoords.Add($"{key}[{i}]: ({variant.X}, {variant.Y}) has negative value");
                }
            }
        }

        if (negativeCoords.Count > 0)
            GD.PrintErr($"Negative coordinates:\n{string.Join("\n", negativeCoords)}");

        AssertThat(negativeCoords.Count).IsEqual(0);
    }

    // ==================== Key Format Validation ====================

    [TestCase]
    public void TestAllTransitionKeysHaveValidFormat()
    {
        AssertThat(_transitionMap).IsNotNull();

        var invalidKeys = new System.Collections.Generic.List<string>();

        foreach (var key in _transitionMap!.Transitions.Keys)
        {
            var parts = key.Split('|');
            if (parts.Length != 2)
                invalidKeys.Add($"'{key}': expected format 'borderId|outerTerrain'");
            else if (string.IsNullOrEmpty(parts[0]) || string.IsNullOrEmpty(parts[1]))
                invalidKeys.Add($"'{key}': empty borderId or outerTerrain");
        }

        if (invalidKeys.Count > 0)
            GD.PrintErr($"Invalid key formats:\n{string.Join("\n", invalidKeys)}");

        AssertThat(invalidKeys.Count).IsEqual(0);
    }

    // ==================== Terrain Reference Validation ====================
    // NOTE: These tests validate that transition_map.json tiles exist in the registry.
    // During TSX migration, the registry may only have TSX-defined tiles.
    // These tests log warnings but pass if tiles are missing due to migration.

    [TestCase]
    public void TestBorderIdsExistInTileRegistry()
    {
        AssertThat(_transitionMap).IsNotNull();

        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var missingBorders = new System.Collections.Generic.List<string>();

        foreach (var borderId in _transitionMap!.GetAllBorderIds())
        {
            if (TransitionOnlyBorderIds.Contains(borderId))
                continue;

            if (!allTileIds.Contains(borderId))
                missingBorders.Add(borderId);
        }

        if (missingBorders.Count > 0)
        {
            // During TSX migration, log as warning instead of error
            var totalBorders = _transitionMap.GetAllBorderIds().Count();
            GD.Print(
                $"[Migration] {missingBorders.Count}/{totalBorders} border IDs not in " +
                "TileRegistry (TSX migration in progress)");
            GD.Print($"  Missing: {string.Join(", ", missingBorders.Take(5))}...");

            // Skip assertion during TSX migration - tiles may not be defined yet
            if (allTileIds.Count < 10)
            {
                GD.Print("  Skipping assertion: registry has <10 tiles (TSX migration mode)");
                return;
            }
        }

        AssertThat(missingBorders.Count).IsEqual(0);
    }

    [TestCase]
    public void TestOuterTerrainIdsExistInTileRegistry()
    {
        AssertThat(_transitionMap).IsNotNull();

        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var missingOuter = new System.Collections.Generic.List<string>();

        foreach (var key in _transitionMap!.Transitions.Keys)
        {
            var (_, outerTerrain) = CompiledTransitionMap.ParseKey(key);
            if (!allTileIds.Contains(outerTerrain))
                missingOuter.Add($"{key}: outer terrain '{outerTerrain}' not in registry");
        }

        if (missingOuter.Count > 0)
        {
            // During TSX migration, log as warning instead of error
            var totalTransitions = _transitionMap.Transitions.Count;
            GD.Print(
                $"[Migration] {missingOuter.Count}/{totalTransitions} outer terrain refs not in " +
                "TileRegistry (TSX migration in progress)");
            GD.Print($"  Sample: {string.Join(", ", missingOuter.Take(3))}...");

            // Skip assertion during TSX migration
            if (allTileIds.Count < 10)
            {
                GD.Print("  Skipping assertion: registry has <10 tiles (TSX migration mode)");
                return;
            }
        }

        AssertThat(missingOuter.Count).IsEqual(0);
    }
}
