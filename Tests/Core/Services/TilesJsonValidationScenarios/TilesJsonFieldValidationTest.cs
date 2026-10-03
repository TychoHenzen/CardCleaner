using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace CardCleaner.Tests.Core.Services.TilesJsonValidationScenarios;

/// <summary>
///     Validates tiles.json required fields, identifiers, passability, layers, auto-tile formats
///     and biomes; split out of TilesJsonValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TilesJsonFieldValidationTest : TilesJsonValidationTestBase
{
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

        var validFormats = new System.Collections.Generic.HashSet<string>(
            new[] { "corner16", "edge16", "blob47" },
            System.StringComparer.OrdinalIgnoreCase);
        if (_tilesDoc!.RootElement.TryGetProperty("autoTileFormats", out var customFormats) &&
            customFormats.ValueKind == JsonValueKind.Array)
        {
            foreach (var customFormat in customFormats.EnumerateArray())
            {
                if (customFormat.TryGetProperty("name", out var nameProperty) &&
                    !string.IsNullOrWhiteSpace(nameProperty.GetString()))
                {
                    validFormats.Add(nameProperty.GetString()!);
                }
            }
        }

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

    // ==================== Biome Validation ====================

    [TestCase]
    public void TestAllBiomeValuesAreValid()
    {
        AssertThat(_tilesDoc).IsNotNull();
        var tiles = _tilesDoc!.RootElement.GetProperty("tiles");

        var validBiomes = new[]
        {
            "plains", "forest", "desert", "tundra", "swamp",
            "mountains", "water", "cave", "volcanic", "magical"
        };
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
}
