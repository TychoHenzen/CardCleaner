using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.AutoTileHelperScenarios;

/// <summary>
///     AutoTileHelperCorner4Edge4BitmaskTest scenarios split out of AutoTileHelperTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileHelperCorner4Edge4BitmaskTest : AutoTileHelperTestBase
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
}
