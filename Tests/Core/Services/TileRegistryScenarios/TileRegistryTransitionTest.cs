using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Core.Services.TileRegistryScenarios;

/// <summary>
///     TileRegistry transition classification, registration edge cases, dominance and variant
///     count scenarios split out of TileRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileRegistryTransitionTest : TileRegistryTestBase
{
    // ==================== Compositable and Fixed Transition Tests ====================

    [TestCase]
    public void TestIsCompositableDetectsWildcardOuterTerrain()
    {
        var tile = new TileDefinition(
            "test_compositable",
            "Test Compositable",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                OuterTerrainId = "*"
            });

        AssertBool(tile.IsCompositable).IsTrue();
        AssertBool(tile.IsFixedTransition).IsFalse();
    }

    [TestCase]
    public void TestIsFixedTransitionDetectsSpecificTerrain()
    {
        var tile = new TileDefinition(
            "test_fixed",
            "Test Fixed",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                OuterTerrainId = "dirt"
            });

        AssertBool(tile.IsFixedTransition).IsTrue();
        AssertBool(tile.IsCompositable).IsFalse();
    }

    [TestCase]
    public void TestNullOuterTerrainIsNeitherCompositableNorFixed()
    {
        var tile = new TileDefinition(
            "test_null_outer",
            "Test Null Outer",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                OuterTerrainId = null
            });

        AssertBool(tile.IsCompositable).IsFalse();
        AssertBool(tile.IsFixedTransition).IsFalse();
    }

    // ==================== Edge Case Tests ====================

    [TestCase]
    public void TestClearRemovesAllTiles()
    {
        _registry.Clear();
        var allTiles = _registry.GetAllTiles().ToList();

        AssertThat(allTiles.Count).IsEqual(0);

        // Reload to restore state for other tests
        _registry.LoadFromData();
    }

    [TestCase]
    public void TestRegisterTileOverwritesDuplicate()
    {
        _registry.Clear();

        var tile1 = new TileDefinition(
            "dup_test",
            "First Version",
            TilePassability.Passable,
            new Vector2I(0, 0));

        var tile2 = new TileDefinition(
            "dup_test",
            "Second Version",
            TilePassability.Solid,
            new Vector2I(1, 1));

        _registry.RegisterTile(tile1);
        _registry.RegisterTile(tile2);

        var retrieved = _registry.GetTile("dup_test");

        AssertThat(retrieved).IsNotNull();
        AssertThat(retrieved!.Name).IsEqual("Second Version");
        AssertThat(retrieved.Passability).IsEqual(TilePassability.Solid);

        // Reload to restore state
        _registry.LoadFromData();
    }

    // ==================== Dominance Tests ====================

    [TestCase]
    public void TestTileDominanceIsPreserved()
    {
        var tile = new TileDefinition(
            "test_dominance",
            "Test Dominance",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                Dominance = 42
            });

        AssertThat(tile.Dominance).IsEqual(42);
    }

    [TestCase]
    public void TestDefaultDominanceIsZero()
    {
        var tile = new TileDefinition(
            "test_default_dom",
            "Test Default Dominance",
            TilePassability.Passable,
            new Vector2I(0, 0));

        AssertThat(tile.Dominance).IsEqual(0);
    }

    // ==================== Auto-Tile Format Tests ====================

    [TestCase]
    public void TestExpectedVariantCountForCorner16()
    {
        var tile = new TileDefinition(
            "test_corner16",
            "Test Corner16",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileFormatName = "corner16"
            });

        AssertThat(tile.ExpectedVariantCount).IsEqual(16);
    }

    [TestCase]
    public void TestExpectedVariantCountForEdge16()
    {
        var tile = new TileDefinition(
            "test_edge16",
            "Test Edge16",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileFormatName = "edge16"
            });

        AssertThat(tile.ExpectedVariantCount).IsEqual(16);
    }

    [TestCase]
    public void TestExpectedVariantCountForBlob47()
    {
        var tile = new TileDefinition(
            "test_blob47",
            "Test Blob47",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileFormatName = "blob47"
            });

        AssertThat(tile.ExpectedVariantCount).IsEqual(47);
    }
}
