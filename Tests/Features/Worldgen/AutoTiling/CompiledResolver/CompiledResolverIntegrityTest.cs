using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.CompiledResolver;

/// <summary>
///     CompiledResolverIntegrityTest scenarios split out of CompiledTransitionResolverTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompiledResolverIntegrityTest : CompiledTransitionResolverTestBase
{
    private static readonly HashSet<string> TransitionOnlyBorderIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "mound6"
    };

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
            if (TransitionOnlyBorderIds.Contains(borderId))
                continue;

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
                missingVariant15.Add(
                    $"{key}: variant[15] is " +
                    $"{(entry.Variants.Length < 16
                        ? "missing (length=" + entry.Variants.Length + ")"
                        : entry.Variants[15] == null
                            ? "null"
                            : "empty")}");
            }
        }

        if (missingVariant15.Count > 0)
            GD.PrintErr(
                "Corner16 transitions missing variant[15] (solid fill):\n" +
                $"{string.Join("\n", missingVariant15)}");

        AssertThat(missingVariant15.Count).IsEqual(0);
    }
}
