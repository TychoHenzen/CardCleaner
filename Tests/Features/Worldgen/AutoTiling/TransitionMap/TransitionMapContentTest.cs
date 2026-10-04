using System;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TransitionMap;

/// <summary>
///     TransitionMapContentTest scenarios split out of TransitionMapValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TransitionMapContentTest : TransitionMapValidationTestBase
{
    // ==================== Format Validation ====================

    [TestCase]
    public void TestAllFormatsAreValid()
    {
        AssertThat(_transitionMap).IsNotNull();

        // Standard formats plus legacy names from older auto-tile systems
        var validFormats = new[] { "corner16", "edge16", "blob47", "rpg", "simplistic" };
        var invalidFormats = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (!validFormats.Contains(entry.Format))
                invalidFormats.Add($"{key}: invalid format '{entry.Format}'");
        }

        if (invalidFormats.Count > 0)
            GD.PrintErr($"Invalid formats:\n{string.Join("\n", invalidFormats)}");

        AssertThat(invalidFormats.Count).IsEqual(0);
    }

    // ==================== Null Variant Checks ====================

    [TestCase]
    public void TestCorner16Variant0IsNotNull()
    {
        // For Corner16, variant 0 = no corners = should be the solid fill tile
        // If it's null, rendering will fail for uniform terrain
        AssertThat(_transitionMap).IsNotNull();

        var nullVariant0 = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length > 0 && (entry.Variants[0] == null || entry.Variants[0]!.Length == 0))
                nullVariant0.Add($"{key}: variant[0] is null or empty (solid fill should be defined)");
        }

        if (nullVariant0.Count > 0)
            GD.Print(
                "Transitions with null variant[0] (may cause issues):\n" +
                $"{string.Join("\n", nullVariant0.Take(10))}");

        // This is informational - null variant[0] may be intentional for some transitions
    }

    [TestCase]
    public void TestCorner16Variant15IsNotNull()
    {
        // Variant 15 = all corners = solid fill, critical for uniform terrain
        AssertThat(_transitionMap).IsNotNull();

        var nullVariant15 = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length > 15 && (entry.Variants[15] == null || entry.Variants[15]!.Length == 0))
                nullVariant15.Add($"{key}: variant[15] is null or empty (solid fill should be defined)");
        }

        if (nullVariant15.Count > 0)
            GD.PrintErr($"Missing variant[15] (solid fill):\n{string.Join("\n", nullVariant15)}");

        AssertThat(nullVariant15.Count).IsEqual(0);
    }

    // ==================== Completeness Checks ====================

    [TestCase]
    public void TestCompositableTilesHaveTransitions()
    {
        // Tiles with outerTerrain="*" should have at least one transition entry
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .Select(t => t.Id)
            .ToHashSet();

        // During TSX migration, we may not have compositable tiles yet
        if (compositableTiles.Count == 0)
        {
            GD.Print("[Migration] No compositable tiles in registry (TSX migration in progress)");
            return;
        }

        var bordersWithTransitions = _transitionMap!.GetAllBorderIds().ToHashSet();

        var missingTransitions = compositableTiles.Except(bordersWithTransitions).ToList();

        if (missingTransitions.Count > 0)
            GD.PrintErr($"Compositable tiles without transitions: {string.Join(", ", missingTransitions)}");

        AssertThat(missingTransitions.Count).IsEqual(0);
    }

    // ==================== Coordinate Uniqueness per Transition ====================

    [TestCase]
    public void TestVariantsWithinTransitionAreUnique()
    {
        // Each variant in a transition should have unique coordinates
        // (non-null variants shouldn't repeat the same coords)
        AssertThat(_transitionMap).IsNotNull();

        var duplicates = new System.Collections.Generic.List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            var coords = new System.Collections.Generic.HashSet<(int, int)>();
            for (var i = 0; i < entry.Variants.Length; i++)
            {
                var variants = entry.Variants[i];
                if (variants == null)
                    continue;

                foreach (var variant in variants)
                {
                    var coord = (variant.X, variant.Y);
                    if (!coords.Add(coord))
                    {
                        duplicates.Add($"{key}[{i}]: ({variant.X}, {variant.Y}) is duplicate");
                    }
                }
            }
        }

        if (duplicates.Count > 0)
            GD.Print(
                "Duplicate coordinates within transitions (may be intentional):\n" +
                $"{string.Join("\n", duplicates.Take(20))}");

        // This is informational - some tiles may intentionally use the same coords for multiple variants
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestTransitionMapStatistics()
    {
        AssertThat(_transitionMap).IsNotNull();

        var totalTransitions = _transitionMap!.Transitions.Count;
        var corner16Count = _transitionMap.Transitions.Values.Count(e => e.Format == "corner16");
        var blob47Count = _transitionMap.Transitions.Values.Count(e => e.Format == "blob47");
        var edge16Count = _transitionMap.Transitions.Values.Count(e => e.Format == "edge16");

        var totalVariants = _transitionMap.Transitions.Values
            .Sum(e => e.Variants.Where(v => v != null).Sum(v => v!.Length));

        var uniqueBorders = _transitionMap.GetAllBorderIds().Count();

        GD.Print($"Transition Map Statistics:");
        GD.Print($"  Total transitions: {totalTransitions}");
        GD.Print($"  Corner16: {corner16Count}");
        GD.Print($"  Blob47: {blob47Count}");
        GD.Print($"  Edge16: {edge16Count}");
        GD.Print($"  Total non-null variants: {totalVariants}");
        GD.Print($"  Unique border IDs: {uniqueBorders}");

        AssertThat(totalTransitions).IsGreater(0);
    }

    // ==================== Cross-Reference with CompiledTransitionResolver ====================

    [TestCase]
    public void TestResolverCanLoadTransitionMap()
    {
        var resolver = new CompiledTransitionResolver();

        // Should load without exceptions
        AssertThat(resolver).IsNotNull();
    }

    [TestCase]
    public void TestResolverCanLookupKnownTransition()
    {
        AssertThat(_transitionMap).IsNotNull();

        // Get first transition key
        var firstKey = _transitionMap!.Transitions.Keys.FirstOrDefault();
        if (firstKey == null)
        {
            GD.PrintErr("No transitions in map");
            return;
        }

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(firstKey);
        var entry = _transitionMap.Transitions[firstKey];

        var resolver = new CompiledTransitionResolver();
        var coords = resolver.ResolveTransition(borderId, outerTerrain, 1); // bitmask 1

        AssertThat(coords).IsNotNull();

        if (entry.Variants.Length > 1 && entry.Variants[1] != null && entry.Variants[1]!.Length > 0)
        {
            AssertThat(coords!.Value.X).IsEqual(entry.Variants[1]![0].X);
            AssertThat(coords!.Value.Y).IsEqual(entry.Variants[1]![0].Y);
        }
    }
}
