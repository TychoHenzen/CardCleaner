using System.Linq;
using Godot;

namespace CardCleaner.Tests.Core.Services.TilesJsonValidationScenarios;

/// <summary>
///     Validates tiles.json cross-references and TileRegistry loading; split out of TilesJsonValidationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TilesJsonReferenceValidationTest : TilesJsonValidationTestBase
{
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
