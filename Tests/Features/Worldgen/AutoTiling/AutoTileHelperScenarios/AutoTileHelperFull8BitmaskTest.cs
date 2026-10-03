using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.AutoTileHelperScenarios;

/// <summary>
///     AutoTileHelperFull8BitmaskTest scenarios split out of AutoTileHelperTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileHelperFull8BitmaskTest : AutoTileHelperTestBase
{
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
}
