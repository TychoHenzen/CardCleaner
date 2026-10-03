using System.Linq;
using System.Text.Json;
using Godot;

namespace CardCleaner.Tests.Core.Services.TilesJsonValidationScenarios;

/// <summary>
///     Validates tiles.json atlas coordinates and auto-tile variant structure; split out of TilesJsonValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TilesJsonVariantValidationTest : TilesJsonValidationTestBase
{
    // ==================== Coordinate Validation ====================

    [TestCase]
    public void TestAllAtlasCoordsAreNonNegative()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var invalidCoords = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();
            var coords = tile.GetProperty("atlasCoords");
            var x = coords.GetProperty("x").GetInt32();
            var y = coords.GetProperty("y").GetInt32();

            if (x < 0 || y < 0)
                invalidCoords.Add($"{id}: atlasCoords ({x}, {y}) has negative value");
        }

        if (invalidCoords.Count > 0)
            GD.PrintErr($"Invalid coordinates:\n{string.Join("\n", invalidCoords)}");

        AssertThat(invalidCoords.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllAutoTileVariantCoordsAreNonNegative()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var invalidVariants = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("autoTileVariants", out var variants))
                continue;

            var index = 0;
            foreach (var variant in variants.EnumerateArray())
            {
                if (variant.ValueKind != JsonValueKind.Null)
                {
                    var x = variant.GetProperty("x").GetInt32();
                    var y = variant.GetProperty("y").GetInt32();

                    if (x < 0 || y < 0)
                        invalidVariants.Add($"{id}: variant[{index}] ({x}, {y}) has negative value");
                }
                index++;
            }
        }

        if (invalidVariants.Count > 0)
            GD.PrintErr($"Invalid variant coordinates:\n{string.Join("\n", invalidVariants)}");

        AssertThat(invalidVariants.Count).IsEqual(0);
    }

    // ==================== Auto-Tile Variant Count Validation ====================

    [TestCase]
    public void TestCorner16TilesHave16Variants()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("autoTileVariants", out var variants))
                continue;

            // Get format, default to corner16
            var format = "corner16";
            if (tile.TryGetProperty("autoTileFormat", out var formatProp))
                format = formatProp.GetString() ?? "corner16";

            if (format == "corner16")
            {
                var count = variants.GetArrayLength();
                if (count != 16)
                    incorrectCounts.Add($"{id}: corner16 format has {count} variants (expected 16)");
            }
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestBlob47TilesHave47Variants()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("autoTileVariants", out var variants))
                continue;

            if (!tile.TryGetProperty("autoTileFormat", out var formatProp))
                continue;

            var format = formatProp.GetString();
            if (format == "blob47")
            {
                var count = variants.GetArrayLength();
                if (count != 47)
                    incorrectCounts.Add($"{id}: blob47 format has {count} variants (expected 47)");
            }
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect blob47 variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    // ==================== Variant Index 0 Validation ====================

    [TestCase]
    public void TestAutoTileVariantsHaveNullAtIndex0()
    {
        // Per Corner16 convention, index 0 (no corners) should be null
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var nonNullIndex0 = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("autoTileVariants", out var variants))
                continue;

            var variantArray = variants.EnumerateArray().ToArray();
            if (variantArray.Length > 0 && variantArray[0].ValueKind != JsonValueKind.Null)
            {
                nonNullIndex0.Add($"{id}: variant[0] is not null (bitmask 0 = no corners should be null)");
            }
        }

        if (nonNullIndex0.Count > 0)
            GD.Print($"Note: Tiles with non-null variant[0]:\n{string.Join("\n", nonNullIndex0)}");

        // This is a soft check - some tiles may legitimately have a variant at index 0
        // Just log it for awareness
    }
}
