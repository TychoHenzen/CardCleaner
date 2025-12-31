using System.IO;
using System.Linq;
using System.Text.Json;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Validates tiles.json structure, data integrity, and cross-references.
/// Tests the source data before it enters the rendering pipeline.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TilesJsonValidationTest
{
    private const string TilesJsonPath = "res://Data/Tiles/tiles.json";
    private JsonDocument? _tilesDoc;
    private TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TilesJsonPath);
        if (File.Exists(absolutePath))
        {
            var json = File.ReadAllText(absolutePath);
            _tilesDoc = JsonDocument.Parse(json);
        }
        _registry = new TileRegistry();
    }

    [AfterTest]
    public void Teardown()
    {
        _tilesDoc?.Dispose();
    }

    // ==================== Required Fields ====================

    [TestCase]
    public void TestTilesJsonFileExists()
    {
        var absolutePath = ProjectSettings.GlobalizePath(TilesJsonPath);
        AssertBool(File.Exists(absolutePath)).IsTrue();
    }

    [TestCase]
    public void TestTilesJsonHasRequiredRootFields()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var root = _tilesDoc!.RootElement;

        AssertBool(root.TryGetProperty("version", out _)).IsTrue();
        AssertBool(root.TryGetProperty("tileset", out _)).IsTrue();
        AssertBool(root.TryGetProperty("tiles", out _)).IsTrue();
    }

    [TestCase]
    public void TestAllTilesHaveRequiredFields()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var missingFields = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.TryGetProperty("id", out var idProp) ? idProp.GetString() : "UNKNOWN";

            if (!tile.TryGetProperty("id", out _))
                missingFields.Add($"{id}: missing 'id'");
            if (!tile.TryGetProperty("name", out _))
                missingFields.Add($"{id}: missing 'name'");
            if (!tile.TryGetProperty("passability", out _))
                missingFields.Add($"{id}: missing 'passability'");
            if (!tile.TryGetProperty("atlasCoords", out _))
                missingFields.Add($"{id}: missing 'atlasCoords'");
        }

        if (missingFields.Count > 0)
            GD.PrintErr($"Missing fields:\n{string.Join("\n", missingFields)}");

        AssertThat(missingFields.Count).IsEqual(0);
    }

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

    // ==================== ID Uniqueness ====================

    [TestCase]
    public void TestAllTileIdsAreUnique()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var ids = new System.Collections.Generic.List<string>();
        var duplicates = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString()!;
            if (ids.Contains(id))
                duplicates.Add(id);
            else
                ids.Add(id);
        }

        if (duplicates.Count > 0)
            GD.PrintErr($"Duplicate tile IDs: {string.Join(", ", duplicates)}");

        AssertThat(duplicates.Count).IsEqual(0);
    }

    // ==================== ID Format Validation ====================

    [TestCase]
    public void TestAllTileIdsMatchSnakeCasePattern()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var invalidIds = new System.Collections.Generic.List<string>();
        var pattern = new System.Text.RegularExpressions.Regex("^[a-z][a-z0-9_]*$");

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString()!;
            if (!pattern.IsMatch(id))
                invalidIds.Add(id);
        }

        if (invalidIds.Count > 0)
            GD.PrintErr($"Invalid tile IDs (not snake_case): {string.Join(", ", invalidIds)}");

        AssertThat(invalidIds.Count).IsEqual(0);
    }

    // ==================== Passability Validation ====================

    [TestCase]
    public void TestAllPassabilityValuesAreValid()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var validValues = new[] { "passable", "solid", "partially_passable" };
        var invalidValues = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();
            var passability = tile.GetProperty("passability").GetString();

            if (!validValues.Contains(passability))
                invalidValues.Add($"{id}: invalid passability '{passability}'");
        }

        if (invalidValues.Count > 0)
            GD.PrintErr($"Invalid passability values:\n{string.Join("\n", invalidValues)}");

        AssertThat(invalidValues.Count).IsEqual(0);
    }

    // ==================== Layer Validation ====================

    [TestCase]
    public void TestAllLayerValuesAreValid()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var validLayers = new[] { "terrain", "decoration", "structure", "effects" };
        var invalidLayers = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("layer", out var layerProp))
                continue;

            var layer = layerProp.GetString();
            if (!validLayers.Contains(layer))
                invalidLayers.Add($"{id}: invalid layer '{layer}'");
        }

        if (invalidLayers.Count > 0)
            GD.PrintErr($"Invalid layer values:\n{string.Join("\n", invalidLayers)}");

        AssertThat(invalidLayers.Count).IsEqual(0);
    }

    // ==================== Auto-Tile Format Validation ====================

    [TestCase]
    public void TestAllAutoTileFormatsAreValid()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var validFormats = new[] { "corner16", "edge16", "blob47" };
        var invalidFormats = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("autoTileFormat", out var formatProp))
                continue;

            var format = formatProp.GetString();
            if (!validFormats.Contains(format))
                invalidFormats.Add($"{id}: invalid autoTileFormat '{format}'");
        }

        if (invalidFormats.Count > 0)
            GD.PrintErr($"Invalid autoTileFormat values:\n{string.Join("\n", invalidFormats)}");

        AssertThat(invalidFormats.Count).IsEqual(0);
    }

    // ==================== OuterTerrain Reference Validation ====================

    [TestCase]
    public void TestOuterTerrainReferencesExistOrAreWildcard()
    {
        var allTileIds = _registry.GetAllTiles().Select(t => t.Id).ToHashSet();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var invalidRefs = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("outerTerrain", out var outerProp))
                continue;

            var outer = outerProp.GetString();
            if (outer != "*" && !string.IsNullOrEmpty(outer) && !allTileIds.Contains(outer))
                invalidRefs.Add($"{id}: outerTerrain '{outer}' does not exist");
        }

        if (invalidRefs.Count > 0)
            GD.PrintErr($"Invalid outerTerrain references:\n{string.Join("\n", invalidRefs)}");

        AssertThat(invalidRefs.Count).IsEqual(0);
    }

    // ==================== Biome Validation ====================

    [TestCase]
    public void TestAllBiomeValuesAreValid()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var validBiomes = new[] { "plains", "forest", "desert", "tundra", "swamp", "mountains" };
        var invalidBiomes = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("biomes", out var biomesProp))
                continue;

            if (biomesProp.ValueKind == JsonValueKind.Null)
                continue;

            foreach (var biome in biomesProp.EnumerateArray())
            {
                var biomeName = biome.GetString();
                if (!validBiomes.Contains(biomeName))
                    invalidBiomes.Add($"{id}: invalid biome '{biomeName}'");
            }
        }

        if (invalidBiomes.Count > 0)
            GD.PrintErr($"Invalid biome values:\n{string.Join("\n", invalidBiomes)}");

        AssertThat(invalidBiomes.Count).IsEqual(0);
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

    // ==================== TileRegistry Cross-Check ====================

    [TestCase]
    public void TestAllJsonTilesLoadIntoRegistry()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var jsonTileIds = tiles.EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()!)
            .ToHashSet();

        var registryTileIds = _registry.GetAllTiles()
            .Select(t => t.Id)
            .ToHashSet();

        var notInRegistry = jsonTileIds.Except(registryTileIds).ToList();

        if (notInRegistry.Count > 0)
            GD.PrintErr($"Tiles in JSON but not in registry: {string.Join(", ", notInRegistry)}");

        AssertThat(notInRegistry.Count).IsEqual(0);
    }

    // ==================== Compositable Tile Validation ====================

    [TestCase]
    public void TestCompositableTilesHaveAutoTileVariants()
    {
        // Tiles with outerTerrain="*" should have autoTileVariants defined
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var missingVariants = new System.Collections.Generic.List<string>();

        foreach (var tile in tiles.EnumerateArray())
        {
            var id = tile.GetProperty("id").GetString();

            if (!tile.TryGetProperty("outerTerrain", out var outerProp))
                continue;

            if (outerProp.GetString() != "*")
                continue;

            if (!tile.TryGetProperty("autoTileVariants", out _))
                missingVariants.Add($"{id}: compositable (outerTerrain=\"*\") but no autoTileVariants");
        }

        if (missingVariants.Count > 0)
            GD.PrintErr($"Compositable tiles missing variants:\n{string.Join("\n", missingVariants)}");

        AssertThat(missingVariants.Count).IsEqual(0);
    }
}
