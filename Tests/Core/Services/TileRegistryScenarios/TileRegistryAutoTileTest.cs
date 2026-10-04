using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Core.Services.TileRegistryScenarios;

/// <summary>
///     TileRegistry auto-tile variant and compiled atlas scenarios split out of TileRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileRegistryAutoTileTest : TileRegistryTestBase
{
    // ==================== Auto-Tile Variant Tests ====================

    [TestCase]
    public void TestCompositableTilesHaveAutoTileVariants()
    {
        // All compositable tiles (outerTerrain="*") must have auto-tile variants
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var missing = new System.Collections.Generic.List<string>();
        foreach (var tile in compositableTiles)
        {
            if (!tile.HasAutoTileVariants)
                missing.Add(tile.Id);
        }

        if (missing.Count > 0)
            GD.PrintErr($"Compositable tiles without auto-tile variants: {string.Join(", ", missing)}");

        AssertThat(missing.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAutoTileVariantsHaveCorrectCount()
    {
        var tilesWithVariants = _registry.GetAllTiles()
            .Where(t => t.HasAutoTileVariants)
            .ToList();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var tile in tilesWithVariants)
        {
            var expected = tile.ExpectedVariantCount;
            var actual = tile.AutoTileVariants!.Length;

            if (actual != expected)
                incorrectCounts.Add($"{tile.Id}: {tile.AutoTileFormatName} expects {expected}, has {actual}");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseForNullVariant()
    {
        // Create tile with a null variant at index 1
        var variants = new Vector2I?[16];
        variants[0] = null; // Index 0 intentionally null
        variants[15] = new Vector2I(10, 10); // Only variant 15 defined

        var tile = new TileDefinition(
            "test_auto",
            "Test Auto",
            TilePassability.Passable,
            new Vector2I(5, 5),
            new TileDefinitionOptions
            {
                AutoTileVariants = variants
            });

        // Index 0 (null) should return base coords
        AssertThat(tile.GetAutoTileCoords(0)).IsEqual(new Vector2I(5, 5));

        // Index 15 (defined) should return variant coords
        AssertThat(tile.GetAutoTileCoords(15)).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseForOutOfBounds()
    {
        var variants = new Vector2I?[16];
        variants[0] = new Vector2I(1, 1);

        var tile = new TileDefinition(
            "test_bounds",
            "Test Bounds",
            TilePassability.Passable,
            new Vector2I(5, 5),
            new TileDefinitionOptions
            {
                AutoTileVariants = variants
            });

        // Out of bounds (negative)
        AssertThat(tile.GetAutoTileCoords(-1)).IsEqual(new Vector2I(5, 5));

        // Out of bounds (too large)
        AssertThat(tile.GetAutoTileCoords(100)).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseWhenNoVariants()
    {
        var tile = new TileDefinition(
            "test_no_variants",
            "Test No Variants",
            TilePassability.Passable,
            new Vector2I(7, 8));

        // Any bitmask should return base coords when no variants defined
        AssertThat(tile.GetAutoTileCoords(0)).IsEqual(new Vector2I(7, 8));
        AssertThat(tile.GetAutoTileCoords(15)).IsEqual(new Vector2I(7, 8));
    }

    // ==================== Compiled Atlas Mode Tests ====================

    [TestCase]
    public void TestRegistryReportsCompiledAtlasStatus()
    {
        // Registry should report whether it's using compiled atlas
        // (Will be true if compiled atlas files exist)
        GD.Print($"UsingCompiledAtlas: {_registry.UsingCompiledAtlas}");
        GD.Print($"CompiledTileSet: {(_registry.CompiledTileSet != null ? "loaded" : "null")}");
        GD.Print($"TilesetPath: {_registry.TilesetPath}");

        // No assertion - just log the state for debugging
    }

    [TestCase]
    public void TestAllTilesHaveValidSourceId()
    {
        var allTiles = _registry.GetAllTiles().ToList();
        var invalidSourceIds = new System.Collections.Generic.List<string>();

        foreach (var tile in allTiles)
        {
            // Source ID should be non-negative
            if (tile.SourceId < 0)
                invalidSourceIds.Add($"{tile.Id}: sourceId={tile.SourceId}");
        }

        if (invalidSourceIds.Count > 0)
            GD.PrintErr($"Tiles with invalid source IDs:\n{string.Join("\n", invalidSourceIds)}");

        AssertThat(invalidSourceIds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllTilesHaveNonNegativeAtlasCoords()
    {
        var allTiles = _registry.GetAllTiles().ToList();
        var invalidCoords = new System.Collections.Generic.List<string>();

        foreach (var tile in allTiles)
        {
            if (tile.AtlasCoords.X < 0 || tile.AtlasCoords.Y < 0)
                invalidCoords.Add($"{tile.Id}: ({tile.AtlasCoords.X}, {tile.AtlasCoords.Y})");
        }

        if (invalidCoords.Count > 0)
            GD.PrintErr($"Tiles with negative atlas coords:\n{string.Join("\n", invalidCoords)}");

        AssertThat(invalidCoords.Count).IsEqual(0);
    }
}
