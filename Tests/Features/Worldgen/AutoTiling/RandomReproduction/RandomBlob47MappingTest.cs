using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.RandomReproduction;

/// <summary>
///     RandomBlob47MappingTest scenarios split out of RandomAutoTileReproductionTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RandomBlob47MappingTest : RandomAutoTileReproductionTestBase
{
    // ==================== Blob47 Index Consistency ====================

    [TestCase]
    public void TestBlob47IndexMappingIsConsistent()
    {
        var valid47 = NeighborBitmask8.GetValid47Masks();
        var inconsistencies = new List<string>();

        for (var expectedIndex = 0; expectedIndex < 47; expectedIndex++)
        {
            var mask = valid47[expectedIndex];
            var actualIndex = NeighborBitmask8.GetBlobIndex(mask);

            if (actualIndex != expectedIndex)
            {
                inconsistencies.Add($"Mask {mask}: expected index {expectedIndex}, got {actualIndex}");
            }
        }

        if (inconsistencies.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 index inconsistencies:\n{string.Join("\n", inconsistencies)}");
        }

        AssertThat(inconsistencies.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47IndexBijectivity()
    {
        // Verify GetBlobIndex is the inverse of indexing into GetValid47Masks
        var valid47 = NeighborBitmask8.GetValid47Masks();
        var errors = new List<string>();

        for (var i = 0; i < 47; i++)
        {
            var mask = valid47[i];
            var recoveredIndex = NeighborBitmask8.GetBlobIndex(mask);
            var recoveredMask = valid47[recoveredIndex];

            if (recoveredMask != mask)
            {
                errors.Add($"Index {i}: mask={mask}, recovered={recoveredMask} via index {recoveredIndex}");
            }
        }

        if (errors.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 bijectivity errors:\n{string.Join("\n", errors)}");
        }

        AssertThat(errors.Count).IsEqual(0);
    }

    // ==================== Transition Map Variant Order ====================

    [TestCase]
    public void TestCorner16VariantOrderMatchesBitmask()
    {
        // For corner16 format, variant[i] should be the tile for bitmask i
        // Verify that variant indices correspond to expected visual patterns

        AssertThat(_transitionMap).IsNotNull();

        var mismatches = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "corner16")
                continue;

            if (entry.Variants.Length != 16)
            {
                mismatches.Add($"{key}: has {entry.Variants.Length} variants (expected 16)");
                continue;
            }

            // Variant 0 = no corners (should be null or base tile)
            // Variant 15 = all corners (solid fill - must exist)
            if (entry.Variants[15] == null)
            {
                mismatches.Add($"{key}: variant[15] (solid fill) is null");
            }
        }

        if (mismatches.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Corner16 variant order issues:\n{string.Join("\n", mismatches)}");
        }

        AssertThat(mismatches.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47VariantOrderMatchesBlobIndex()
    {
        AssertThat(_transitionMap).IsNotNull();

        var valid47 = NeighborBitmask8.GetValid47Masks();
        var mismatches = new List<string>();

        foreach (var (key, entry) in _transitionMap!.Transitions)
        {
            if (entry.Format != "blob47")
                continue;

            if (entry.Variants.Length != 47)
            {
                mismatches.Add($"{key}: has {entry.Variants.Length} variants (expected 47)");
                continue;
            }

            // Verify critical indices
            // Index 0 = mask 0 = isolated tile
            // Index 46 = mask 255 = solid fill
            if (valid47[0] != 0)
                mismatches.Add($"Blob47 index 0 should map to mask 0, but maps to {valid47[0]}");

            if (valid47[46] != 255)
                mismatches.Add($"Blob47 index 46 should map to mask 255, but maps to {valid47[46]}");
        }

        if (mismatches.Count > 0)
        {
            GD.PrintErr($"RANDOM PATTERN CAUSE - Blob47 variant order issues:\n{string.Join("\n", mismatches)}");
        }

        AssertThat(mismatches.Count).IsEqual(0);
    }
}
