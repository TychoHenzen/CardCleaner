using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Validates all 47 blob patterns, their bitmask definitions, and visual connectivity rules.
/// The blob47 system reduces 256 possible 8-neighbor combinations to 47 valid ones by enforcing
/// the constraint that corners are only valid when both adjacent edges are set.
///
/// Pattern visualization (3x3 grid with center tile at position (1,1)):
///   NW  N  NE
///   W  [X] E
///   SW  S  SE
///
/// Bit positions: N=0, NE=1, E=2, SE=3, S=4, SW=5, W=6, NW=7
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class Blob47PatternValidationTest
{
    private const string TransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";

    // ==================== Valid Mask Count ====================

    [TestCase]
    public void TestExactly47ValidMasks()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();
        AssertThat(validMasks.Count).IsEqual(47);
    }

    // ==================== Corner Constraint Validation ====================

    [TestCase]
    public void TestNorthEastCornerRequiresNorthAndEast()
    {
        // NE corner alone is invalid
        var neOnly = NeighborBitmask8.NorthEast;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neOnly)).IsFalse();

        // NE with N but not E is invalid
        var neN = NeighborBitmask8.NorthEast | NeighborBitmask8.North;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neN)).IsFalse();

        // NE with E but not N is invalid
        var neE = NeighborBitmask8.NorthEast | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neE)).IsFalse();

        // NE with both N and E is valid
        var neNE = NeighborBitmask8.NorthEast | NeighborBitmask8.North | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(neNE)).IsTrue();
    }

    [TestCase]
    public void TestSouthEastCornerRequiresSouthAndEast()
    {
        var seOnly = NeighborBitmask8.SouthEast;
        AssertBool(NeighborBitmask8.IsValidBlobMask(seOnly)).IsFalse();

        var seSE = NeighborBitmask8.SouthEast | NeighborBitmask8.South | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(seSE)).IsTrue();
    }

    [TestCase]
    public void TestSouthWestCornerRequiresSouthAndWest()
    {
        var swOnly = NeighborBitmask8.SouthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(swOnly)).IsFalse();

        var swSW = NeighborBitmask8.SouthWest | NeighborBitmask8.South | NeighborBitmask8.West;
        AssertBool(NeighborBitmask8.IsValidBlobMask(swSW)).IsTrue();
    }

    [TestCase]
    public void TestNorthWestCornerRequiresNorthAndWest()
    {
        var nwOnly = NeighborBitmask8.NorthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(nwOnly)).IsFalse();

        var nwNW = NeighborBitmask8.NorthWest | NeighborBitmask8.North | NeighborBitmask8.West;
        AssertBool(NeighborBitmask8.IsValidBlobMask(nwNW)).IsTrue();
    }

    // ==================== Normalization ====================

    [TestCase]
    public void TestNormalizationClearsInvalidCorners()
    {
        // Raw mask with all corners but no edges
        var raw = NeighborBitmask8.AllCorners; // 170
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // All corners should be cleared since no edges
        AssertThat(normalized).IsEqual(0);
    }

    [TestCase]
    public void TestNormalizationPreservesValidCorners()
    {
        // Raw mask with N+E edges and NE corner
        var raw = NeighborBitmask8.North | NeighborBitmask8.East | NeighborBitmask8.NorthEast;
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // All bits should be preserved
        AssertThat(normalized).IsEqual(raw);
    }

    [TestCase]
    public void TestNormalizationClearsPartiallyValidCorners()
    {
        // N + NE (NE invalid because no E)
        var raw = NeighborBitmask8.North | NeighborBitmask8.NorthEast;
        var normalized = NeighborBitmask8.NormalizeToBlobMask(raw);

        // Only N should remain
        AssertThat(normalized).IsEqual(NeighborBitmask8.North);
    }

    [TestCase]
    public void TestNormalizationIsIdempotent()
    {
        // Normalizing a valid mask should return the same mask
        var validMasks = NeighborBitmask8.GetValid47Masks();

        foreach (var mask in validMasks)
        {
            var normalized = NeighborBitmask8.NormalizeToBlobMask(mask);
            AssertThat(normalized).IsEqual(mask);
        }
    }

    // ==================== GetBlobIndex Tests ====================

    [TestCase]
    public void TestGetBlobIndexReturnsUniqueIndices()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();
        var indices = new HashSet<int>();

        foreach (var mask in validMasks)
        {
            var index = NeighborBitmask8.GetBlobIndex(mask);
            AssertThat(index).IsGreaterEqual(0);
            AssertThat(index).IsLess(47);
            AssertBool(indices.Add(index)).IsTrue(); // Should be unique
        }

        AssertThat(indices.Count).IsEqual(47);
    }

    [TestCase]
    public void TestGetBlobIndexReturnsNegativeForInvalid()
    {
        // Mask with NE corner but no N or E edges
        var invalidMask = NeighborBitmask8.NorthEast;
        var index = NeighborBitmask8.GetBlobIndex(invalidMask);

        AssertThat(index).IsEqual(-1);
    }

    [TestCase]
    public void TestGetBlobIndexZeroForEmptyMask()
    {
        var index = NeighborBitmask8.GetBlobIndex(0);
        AssertThat(index).IsEqual(0); // 0 is always the first valid mask
    }

    [TestCase]
    public void TestGetBlobIndex46ForFullMask()
    {
        var index = NeighborBitmask8.GetBlobIndex(NeighborBitmask8.All);
        AssertThat(index).IsEqual(46); // 255 (all bits) is the last valid mask
    }

    // ==================== Edge-Only Masks ====================

    [TestCase]
    public void TestAllEdgeOnlyMasksAreValid()
    {
        // All 16 combinations of edges (without corners) should be valid
        for (var i = 0; i < 16; i++)
        {
            var mask = 0;
            if ((i & 1) != 0) mask |= NeighborBitmask8.North;
            if ((i & 2) != 0) mask |= NeighborBitmask8.East;
            if ((i & 4) != 0) mask |= NeighborBitmask8.South;
            if ((i & 8) != 0) mask |= NeighborBitmask8.West;

            AssertBool(NeighborBitmask8.IsValidBlobMask(mask)).IsTrue();
        }
    }

    // ==================== Symmetry Tests ====================

    [TestCase]
    public void TestHorizontallySymmetricMasks()
    {
        // N alone vs S alone should both be valid
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.North)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.South)).IsTrue();

        // E alone vs W alone should both be valid
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.East)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(NeighborBitmask8.West)).IsTrue();
    }

    [TestCase]
    public void TestDiagonallyOppositeCornerMasks()
    {
        // NE with N+E vs SW with S+W should both be valid
        var neValid = NeighborBitmask8.NorthEast | NeighborBitmask8.North | NeighborBitmask8.East;
        var swValid = NeighborBitmask8.SouthWest | NeighborBitmask8.South | NeighborBitmask8.West;

        AssertBool(NeighborBitmask8.IsValidBlobMask(neValid)).IsTrue();
        AssertBool(NeighborBitmask8.IsValidBlobMask(swValid)).IsTrue();
    }

    // ==================== Common Patterns ====================

    [TestCase]
    public void TestSolidFillMaskIsValid()
    {
        // All edges + all corners = solid fill = 255
        var solidFill = NeighborBitmask8.All;
        AssertBool(NeighborBitmask8.IsValidBlobMask(solidFill)).IsTrue();
        AssertThat(NeighborBitmask8.GetBlobIndex(solidFill)).IsGreaterEqual(0);
    }

    [TestCase]
    public void TestIsolatedTileMaskIsValid()
    {
        // No neighbors = isolated tile = 0
        AssertBool(NeighborBitmask8.IsValidBlobMask(0)).IsTrue();
        AssertThat(NeighborBitmask8.GetBlobIndex(0)).IsEqual(0);
    }

    [TestCase]
    public void TestHorizontalStripMask()
    {
        // W + E (horizontal strip)
        var hStrip = NeighborBitmask8.West | NeighborBitmask8.East;
        AssertBool(NeighborBitmask8.IsValidBlobMask(hStrip)).IsTrue();
    }

    [TestCase]
    public void TestVerticalStripMask()
    {
        // N + S (vertical strip)
        var vStrip = NeighborBitmask8.North | NeighborBitmask8.South;
        AssertBool(NeighborBitmask8.IsValidBlobMask(vStrip)).IsTrue();
    }

    [TestCase]
    public void TestCornerPieceMask()
    {
        // Bottom-right corner: N + W + NW
        var blCorner = NeighborBitmask8.North | NeighborBitmask8.West | NeighborBitmask8.NorthWest;
        AssertBool(NeighborBitmask8.IsValidBlobMask(blCorner)).IsTrue();
    }

    // ==================== Transition Map Blob47 Validation ====================

    [TestCase]
    public void TestBlob47TransitionsInMapHaveCorrectVariantCount()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (!File.Exists(absolutePath))
        {
            GD.PrintErr("transition_map.json not found");
            return;
        }

        var json = File.ReadAllText(absolutePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("transitions", out var transitions))
        {
            GD.PrintErr("No transitions in map");
            return;
        }

        var incorrectCounts = new List<string>();

        foreach (var transition in transitions.EnumerateObject())
        {
            var entry = transition.Value;
            if (!entry.TryGetProperty("format", out var formatProp))
                continue;

            var format = formatProp.GetString();
            if (format != "blob47")
                continue;

            if (!entry.TryGetProperty("variants", out var variants))
            {
                incorrectCounts.Add($"{transition.Name}: no variants array");
                continue;
            }

            var count = variants.GetArrayLength();
            if (count != 47)
                incorrectCounts.Add($"{transition.Name}: blob47 has {count} variants (expected 47)");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect blob47 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47TransitionsHaveVariantAtIndex0And46()
    {
        // Index 0 = isolated (0), Index 46 = solid fill (255)
        var absolutePath = ProjectSettings.GlobalizePath(TransitionMapPath);
        if (!File.Exists(absolutePath))
            return;

        var json = File.ReadAllText(absolutePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("transitions", out var transitions))
            return;

        var missingVariants = new List<string>();

        foreach (var transition in transitions.EnumerateObject())
        {
            var entry = transition.Value;
            if (!entry.TryGetProperty("format", out var formatProp))
                continue;

            if (formatProp.GetString() != "blob47")
                continue;

            if (!entry.TryGetProperty("variants", out var variants))
                continue;

            var variantArray = variants.EnumerateArray().ToArray();
            if (variantArray.Length < 47)
                continue;

            if (variantArray[0].ValueKind == JsonValueKind.Null)
                missingVariants.Add($"{transition.Name}: variant[0] (isolated) is null");

            if (variantArray[46].ValueKind == JsonValueKind.Null)
                missingVariants.Add($"{transition.Name}: variant[46] (solid fill) is null");
        }

        if (missingVariants.Count > 0)
            GD.PrintErr($"Missing critical blob47 variants:\n{string.Join("\n", missingVariants)}");

        AssertThat(missingVariants.Count).IsEqual(0);
    }

    // ==================== Compute Tests ====================

    [TestCase]
    public void TestComputeWithIsolatedTile()
    {
        var tileIds = new string[3, 3];
        tileIds[1, 1] = "test";

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // No neighbors match, so mask should be 0
        AssertThat(mask).IsEqual(0);
    }

    [TestCase]
    public void TestComputeWithSurroundedTile()
    {
        var tileIds = new string[3, 3];
        for (var y = 0; y < 3; y++)
            for (var x = 0; x < 3; x++)
                tileIds[y, x] = "test";

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // All neighbors match, so mask should be 255
        AssertThat(mask).IsEqual(255);
    }

    [TestCase]
    public void TestComputeWithHorizontalNeighbors()
    {
        var tileIds = new string[3, 3];
        tileIds[1, 0] = "test"; // West
        tileIds[1, 1] = "test"; // Center
        tileIds[1, 2] = "test"; // East

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // W + E
        var expected = NeighborBitmask8.West | NeighborBitmask8.East;
        AssertThat(mask).IsEqual(expected);
    }

    [TestCase]
    public void TestComputeWithCornerNeighbors()
    {
        var tileIds = new string[3, 3];
        tileIds[0, 0] = "test"; // NW
        tileIds[0, 1] = "test"; // N
        tileIds[1, 0] = "test"; // W
        tileIds[1, 1] = "test"; // Center

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // N + W + NW (all valid because N and W are present)
        var expected = NeighborBitmask8.North | NeighborBitmask8.West | NeighborBitmask8.NorthWest;
        AssertThat(mask).IsEqual(expected);
    }

    [TestCase]
    public void TestComputeNormalizesInvalidCorners()
    {
        var tileIds = new string[3, 3];
        tileIds[0, 0] = "test"; // NW (but no N or W edges!)
        tileIds[1, 1] = "test"; // Center

        var mask = NeighborBitmask8.Compute(new Vector2I(1, 1), "test", tileIds, new Vector2I(3, 3));

        // NW corner should be cleared because N and W edges aren't set
        AssertThat(mask).IsEqual(0);
    }

    // ==================== Statistics ====================

    [TestCase]
    public void TestValid47MasksStatistics()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();

        var edgesOnlyCount = validMasks.Count(m => (m & NeighborBitmask8.AllCorners) == 0);
        var fullCornersCount = validMasks.Count(m => (m & NeighborBitmask8.AllCorners) == NeighborBitmask8.AllCorners);

        GD.Print("Blob47 Mask Statistics:");
        GD.Print($"  Total valid masks: {validMasks.Count}");
        GD.Print($"  Edges-only masks: {edgesOnlyCount}"); // Should be 16 (2^4)
        GD.Print($"  Full corners masks: {fullCornersCount}"); // Only mask 255

        AssertThat(edgesOnlyCount).IsEqual(16);
        AssertThat(fullCornersCount).IsEqual(1);
    }

    // ==================== Visual Reference ====================

    [TestCase]
    public void TestLogAllValidMasksWithDescriptions()
    {
        var validMasks = NeighborBitmask8.GetValid47Masks();

        GD.Print("All 47 Valid Blob Masks:");
        for (var i = 0; i < validMasks.Count; i++)
        {
            var mask = validMasks[i];
            var description = NeighborBitmask8.GetDescription(mask);
            GD.Print($"  Index {i,2}: {description}");
        }

        // Informational - just verify we logged 47
        AssertThat(validMasks.Count).IsEqual(47);
    }
}
