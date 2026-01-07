using System;
using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Integration tests for AutoTileHelper.
/// Validates bitmask computation and variant retrieval using different auto-tile formats.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileHelperTest
{
    // ==================== ComputeBitmask - Corner4 Format ====================

    /// <summary>
    /// Test ComputeBitmask with Corner4 format - isolated tile (no neighbors).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Corner4_NoNeighbors_Returns0()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        Func<Vector2I, string?> getTileId = _ => null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(bitmask).IsEqual(0);
    }

    /// <summary>
    /// Test ComputeBitmask with Corner4 format - NE neighbor only.
    /// NE = bit 0 in NeighborBitmaskCorner (value 1).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Corner4_NENeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        var grassPositions = new HashSet<Vector2I> { new(6, 4) }; // NE of (5,5)
        Func<Vector2I, string?> getTileId = pos => grassPositions.Contains(pos) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        // NeighborBitmaskCorner: NE=1
        AssertThat(bitmask).IsEqual(1);
    }

    /// <summary>
    /// Test ComputeBitmask with Corner4 format - SE neighbor only.
    /// SE = bit 1 in NeighborBitmaskCorner (value 2).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Corner4_SENeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        var grassPositions = new HashSet<Vector2I> { new(6, 6) }; // SE of (5,5)
        Func<Vector2I, string?> getTileId = pos => grassPositions.Contains(pos) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        // NeighborBitmaskCorner: SE=2
        AssertThat(bitmask).IsEqual(2);
    }

    /// <summary>
    /// Test ComputeBitmask with Corner4 format - all 4 diagonal neighbors.
    /// NE=1, SE=2, SW=4, NW=8 → total = 15.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Corner4_AllDiagonalNeighbors()
    {
        var tileDef = CreateTileDefinition("grass", "corner16");
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(6, 4), // NE
            new(6, 6), // SE
            new(4, 6), // SW
            new(4, 4)  // NW
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // All corners: 1 + 2 + 4 + 8 = 15
        AssertThat(bitmask).IsEqual(15);
    }

    // ==================== ComputeBitmask - Edge4 Format ====================

    /// <summary>
    /// Test ComputeBitmask with Edge4 format - N neighbor only.
    /// N = bit 0 in NeighborBitmask (value 1).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Edge4_NNeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "edge16");
        var grassPositions = new HashSet<Vector2I> { new(5, 4) }; // N of (5,5)
        Func<Vector2I, string?> getTileId = pos => grassPositions.Contains(pos) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        // NeighborBitmask (Edge4): N=1
        AssertThat(bitmask).IsEqual(1);
    }

    /// <summary>
    /// Test ComputeBitmask with Edge4 format - E neighbor only.
    /// E = bit 1 in NeighborBitmask (value 2).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Edge4_ENeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "edge16");
        var grassPositions = new HashSet<Vector2I> { new(6, 5) }; // E of (5,5)
        Func<Vector2I, string?> getTileId = pos => grassPositions.Contains(pos) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        // NeighborBitmask (Edge4): E=2
        AssertThat(bitmask).IsEqual(2);
    }

    /// <summary>
    /// Test ComputeBitmask with Edge4 format - all 4 cardinal neighbors.
    /// N=1, E=2, S=4, W=8 → total = 15.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Edge4_AllCardinalNeighbors()
    {
        var tileDef = CreateTileDefinition("grass", "edge16");
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(5, 4), // N
            new(6, 5), // E
            new(5, 6), // S
            new(4, 5)  // W
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // All edges: 1 + 2 + 4 + 8 = 15
        AssertThat(bitmask).IsEqual(15);
    }

    // ==================== ComputeBitmask - Full8 (Blob) Format ====================

    /// <summary>
    /// Test ComputeBitmask with Full8 format - N neighbor only.
    /// N = bit 0 in NeighborBitmask8 (value 1).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Full8_NNeighbor()
    {
        var tileDef = CreateTileDefinition("grass", "blob47");
        var grassPositions = new HashSet<Vector2I> { new(5, 4) }; // N of (5,5)
        Func<Vector2I, string?> getTileId = pos => grassPositions.Contains(pos) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(new Vector2I(5, 5), tileDef, getTileId);

        // NeighborBitmask8: N=1
        AssertThat(bitmask).IsEqual(1);
    }

    /// <summary>
    /// Test ComputeBitmask with Full8 format - all 8 neighbors.
    /// N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128 → total = 255.
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Full8_AllNeighbors()
    {
        var tileDef = CreateTileDefinition("grass", "blob47");
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(5, 4), // N
            new(6, 4), // NE
            new(6, 5), // E
            new(6, 6), // SE
            new(5, 6), // S
            new(4, 6), // SW
            new(4, 5), // W
            new(4, 4)  // NW
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // All 8: 1 + 2 + 4 + 8 + 16 + 32 + 64 + 128 = 255
        AssertThat(bitmask).IsEqual(255);
    }

    /// <summary>
    /// Test ComputeBitmask with Full8 format - N and E without NE.
    /// This tests edge-only combination (no corner).
    /// </summary>
    [TestCase]
    public void TestComputeBitmask_Full8_NAndEWithoutCorner()
    {
        var tileDef = CreateTileDefinition("grass", "blob47");
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(5, 4), // N (value 1)
            new(6, 5)  // E (value 4)
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var bitmask = AutoTileHelper.ComputeBitmask(pos, tileDef, getTileId);

        // N + E = 1 + 4 = 5
        AssertThat(bitmask).IsEqual(5);
    }

    // ==================== GetAutoTileVariant ====================

    /// <summary>
    /// Test GetAutoTileVariant returns variant when available.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileVariant_ReturnsVariantWhenAvailable()
    {
        var variants = CreateVariantArray(16);
        variants[5] = new Vector2I(10, 5);
        var tileDef = CreateTileDefinitionWithVariants("grass", "corner16", variants);

        // Setup: SE + SW neighbors = bitmask 6 (2 + 4)
        var pos = new Vector2I(5, 5);
        var grassPositions = new HashSet<Vector2I>
        {
            new(6, 6), // SE (value 2)
            new(4, 6)  // SW (value 4)
        };
        Func<Vector2I, string?> getTileId = p => grassPositions.Contains(p) ? "grass" : null;

        var variant = AutoTileHelper.GetAutoTileVariant(pos, tileDef, getTileId);

        // Bitmask 6 should return a variant (from format, not from AutoTileVariants array)
        // Note: GetAutoTileVariant delegates to tileDef.GetVariantDefinition which uses the format registry
        // For built-in formats, variants use placeholder coords (bitmask, 0)
        AssertThat(variant).IsNotNull();
    }

    /// <summary>
    /// Test GetAutoTileVariant returns null when tile has no variants.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileVariant_ReturnsNullWhenNoVariants()
    {
        var tileDef = CreateTileDefinition("grass", "corner16", hasVariants: false);
        Func<Vector2I, string?> getTileId = _ => "grass";

        var variant = AutoTileHelper.GetAutoTileVariant(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(variant).IsNull();
    }

    // ==================== GetAutoTileCoords ====================

    /// <summary>
    /// Test GetAutoTileCoords returns base coords when no variants.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileCoords_ReturnsBaseCoordsWhenNoVariants()
    {
        var tileDef = CreateTileDefinition("grass", "corner16", hasVariants: false);
        Func<Vector2I, string?> getTileId = _ => "grass";

        var coords = AutoTileHelper.GetAutoTileCoords(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(coords).IsEqual(tileDef.AtlasCoords);
    }

    /// <summary>
    /// Test GetAutoTileCoords returns variant coords when available.
    /// </summary>
    [TestCase]
    public void TestGetAutoTileCoords_ReturnsVariantCoords()
    {
        var variants = CreateVariantArray(16);
        variants[0] = new Vector2I(99, 99); // Bitmask 0 (isolated)
        var tileDef = CreateTileDefinitionWithVariants("grass", "corner16", variants);
        Func<Vector2I, string?> getTileId = _ => null; // No neighbors = bitmask 0

        var coords = AutoTileHelper.GetAutoTileCoords(new Vector2I(5, 5), tileDef, getTileId);

        AssertThat(coords).IsEqual(new Vector2I(99, 99));
    }

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
            autoTileFormatName: "nonexistent_format_xyz");

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

    // ==================== Helper Methods ====================

    private static TileDefinition CreateTileDefinition(string id, string formatName, bool hasVariants = true)
    {
        var variants = hasVariants ? CreateVariantArray(formatName == "blob47" ? 47 : 16) : null;

        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            new Vector2I(0, 0),
            autoTileVariants: variants,
            autoTileFormatName: formatName);
    }

    private static TileDefinition CreateTileDefinitionWithVariants(
        string id,
        string formatName,
        Vector2I?[] variants)
    {
        return new TileDefinition(
            id,
            id,
            TilePassability.Passable,
            new Vector2I(0, 0),
            autoTileVariants: variants,
            autoTileFormatName: formatName);
    }

    private static Vector2I?[] CreateVariantArray(int count)
    {
        var variants = new Vector2I?[count];
        for (var i = 0; i < count; i++)
        {
            variants[i] = new Vector2I(i, 0);
        }
        return variants;
    }
}
