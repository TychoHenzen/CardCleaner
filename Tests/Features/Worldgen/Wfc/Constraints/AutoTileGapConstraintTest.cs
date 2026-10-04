using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Tests.Mocks;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Tests for AutoTileGapConstraint.
/// Validates that different auto-tile types cannot be adjacent (requires 1-tile gap),
/// while same auto-tile types and gap tiles can be adjacent to anything.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class AutoTileGapConstraintTest
{
    private ITileRegistry _tileRegistry = null!;
    private AutoTileGapConstraint _constraint = null!;
    private WfcGrid _grid = null!;

    // Test tiles - grass/stone are auto-tiles, dirt/rock are gap tiles
    private static readonly string[] AllTiles = { "grass", "stone", "dirt", "rock" };

    [BeforeTest]
    public void Setup()
    {
        // Use mock registry with controlled tile properties
        _tileRegistry = MockTileRegistry.CreateWithTestTiles();
        _constraint = new AutoTileGapConstraint(_tileRegistry);
        _grid = new WfcGrid(5, 5, AllTiles);
    }

    // ========== Auto-Tile Adjacent to DIFFERENT Auto-Tile: BANNED ==========

    [TestCase]
    public void AutoTileAdjacentToDifferentAutoTile_ReturnsZero()
    {
        // Collapse a neighbor to auto-tile "grass"
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");

        // Try to place different auto-tile "stone" adjacent
        var context = CreateContext(new Vector2I(2, 2), "stone");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(0.0f);
    }

    [TestCase]
    public void AutoTileAdjacentToDifferentAutoTile_AllDirections_ReturnsZero()
    {
        // Place "grass" auto-tile at center
        _grid.GetCell(new Vector2I(2, 2)).CollapseTo("grass");

        // Try to place "stone" in each cardinal direction
        var directions = new[]
        {
            new Vector2I(2, 1), // North
            new Vector2I(2, 3), // South
            new Vector2I(1, 2), // West
            new Vector2I(3, 2), // East
        };

        foreach (var dir in directions)
        {
            var context = CreateContext(dir, "stone");
            var result = _constraint.GetProbabilityModifier(context);
            AssertFloat(result).IsEqual(0.0f);
        }
    }

    // ========== Auto-Tile Adjacent to SAME Auto-Tile: ALLOWED ==========

    [TestCase]
    public void AutoTileAdjacentToSameAutoTile_ReturnsOne()
    {
        // Collapse neighbor to "grass" auto-tile
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");

        // Try to place same auto-tile "grass" adjacent
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void AutoTileRegionGrowth_MultipleNeighbors_ReturnsOne()
    {
        // Setup: Create a 2x2 grass region
        _grid.GetCell(new Vector2I(1, 1)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(1, 2)).CollapseTo("grass");

        // Extend region at (2,2) - should be allowed
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    // ========== Gap Tile (Non-Auto-Tile) Adjacent to Anything: ALLOWED ==========

    [TestCase]
    public void GapTileAdjacentToAutoTile_ReturnsOne()
    {
        // Collapse neighbor to auto-tile
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");

        // Try to place gap tile "dirt" (not an auto-tile) adjacent
        var context = CreateContext(new Vector2I(2, 2), "dirt");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void GapTileAdjacentToAnotherGapTile_ReturnsOne()
    {
        // Collapse neighbor to gap tile
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("dirt");

        // Try to place another gap tile adjacent
        var context = CreateContext(new Vector2I(2, 2), "rock");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void GapTileBetweenDifferentAutoTiles_ReturnsOne()
    {
        // Setup: Two different auto-tiles with a gap position between them
        _grid.GetCell(new Vector2I(1, 2)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(3, 2)).CollapseTo("stone");

        // Position (2,2) is not directly adjacent to either auto-tile in this grid setup
        // But let's test the gap tile can go anywhere
        var context = CreateContext(new Vector2I(2, 2), "dirt");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    // ========== Edge Cases ==========

    [TestCase]
    public void AutoTileWithNoCollapsedNeighbors_ReturnsOne()
    {
        // No neighbors collapsed yet - auto-tile should be allowed
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void AutoTileWithOnlyGapTileNeighbors_ReturnsOne()
    {
        // All neighbors are gap tiles (non-auto-tiles)
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("dirt");
        _grid.GetCell(new Vector2I(2, 3)).CollapseTo("rock");
        _grid.GetCell(new Vector2I(1, 2)).CollapseTo("dirt");
        _grid.GetCell(new Vector2I(3, 2)).CollapseTo("rock");

        // Auto-tile should be allowed since neighbors are all gap tiles
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void AutoTileWithMixedNeighbors_SameAutoTileAndGap_ReturnsOne()
    {
        // One neighbor is same auto-tile, others are gap tiles
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(2, 3)).CollapseTo("dirt");
        _grid.GetCell(new Vector2I(1, 2)).CollapseTo("rock");

        // Same auto-tile should be allowed
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void AutoTileWithMixedNeighbors_DifferentAutoTileAndGap_ReturnsZero()
    {
        // One neighbor is different auto-tile, others are gap tiles
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("stone");  // Different auto-tile
        _grid.GetCell(new Vector2I(2, 3)).CollapseTo("dirt");   // Gap tile
        _grid.GetCell(new Vector2I(1, 2)).CollapseTo("rock");   // Gap tile

        // Different auto-tile adjacent = banned
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(0.0f);
    }

    // ========== Helper Methods ==========

    private WfcConstraintContext CreateContext(Vector2I position, string tileId)
    {
        return new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(position),
            TileId = tileId,
            Topology = _grid,
            Rng = null
        };
    }
}
