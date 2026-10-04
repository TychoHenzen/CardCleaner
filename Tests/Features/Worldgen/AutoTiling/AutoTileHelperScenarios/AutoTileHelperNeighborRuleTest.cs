using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.AutoTileHelperScenarios;

/// <summary>
///     AutoTileHelperNeighborRuleTest scenarios split out of AutoTileHelperTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileHelperNeighborRuleTest : AutoTileHelperTestBase
{
    // ==================== Null/Unknown Format Handling ====================

    /// <summary>
    /// Test ComputeBitmask defaults to Corner4 when format is unknown.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_DefaultsToCorner4ForUnknownFormat()
    {
        // Create tile with non-existent format
        var tileDef = new TileDefinition(
            "test",
            "Test Tile",
            TilePassability.Passable,
            new Vector2I(0, 0),
            new TileDefinitionOptions
            {
                AutoTileFormatName = "nonexistent_format_xyz"
            });

        var pos = new Vector2I(5, 5);
        // Use NE neighbor which is diagonal (only Corner4 checks diagonals as primary)
        var neighborPositions = new HashSet<Vector2I> { new(6, 4) }; // NE
        Func<Vector2I, string?> getTileId = p => neighborPositions.Contains(p) ? "test" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // Should default to Corner4, which checks diagonal corners
        // NE in Corner4 = bit 0 = 1
        AssertThat(bitmask).IsEqual(1);
    }

    // ==================== Different Tile ID Handling ====================

    /// <summary>
    /// Test that different tile IDs don't count as neighbors.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_DifferentTileIdNotNeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        var pos = new Vector2I(5, 5);

        // All corners have different tile types
        var tiles = new Dictionary<Vector2I, string>
        {
            [new Vector2I(6, 4)] = "dirt",
            [new Vector2I(6, 6)] = "sand",
            [new Vector2I(4, 6)] = "water",
            [new Vector2I(4, 4)] = "stone"
        };
        Func<Vector2I, string?> getTileId = p => tiles.TryGetValue(p, out var id) ? id : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // No matching neighbors = 0
        AssertThat(bitmask).IsEqual(0);
    }

    /// <summary>
    /// Test that only same tile ID counts as neighbor.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_OnlySameTileIdIsNeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        var pos = new Vector2I(5, 5);

        // Mix of grass and other tiles
        var tiles = new Dictionary<Vector2I, string>
        {
            [new Vector2I(6, 4)] = "grass", // NE - matches
            [new Vector2I(6, 6)] = "dirt",  // SE - doesn't match
            [new Vector2I(4, 6)] = "grass", // SW - matches
            [new Vector2I(4, 4)] = "sand"   // NW - doesn't match
        };
        Func<Vector2I, string?> getTileId = p => tiles.TryGetValue(p, out var id) ? id : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // Only NE (1) and SW (4) match = 5
        AssertThat(bitmask).IsEqual(5);
    }
}
