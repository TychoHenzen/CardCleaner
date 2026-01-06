using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
[RequireGodotRuntime]
public class WfcGridTest
{
    private RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
    }

    [TestCase]
    public void TestGridDimensions()
    {
        var grid = new WfcGrid(5, 3, new[] { "grass", "dirt" });

        AssertThat(grid.Width).IsEqual(5);
        AssertThat(grid.Height).IsEqual(3);
    }

    [TestCase]
    public void TestAllCellsInitializedWithAllTiles()
    {
        var tiles = new[] { "grass", "dirt", "sand" };
        var grid = new WfcGrid(3, 3, tiles);

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                var cell = grid.GetCell(x, y);
                AssertThat(cell.GetPossibleTiles().Count).IsEqual(3);
            }
        }
    }

    [TestCase]
    public void TestGetCellByVector()
    {
        var grid = new WfcGrid(3, 3, new[] { "grass" });
        var cell1 = grid.GetCell(1, 2);
        var cell2 = grid.GetCell(new Vector2I(1, 2));

        AssertThat(cell1).IsSame(cell2);
    }

    [TestCase]
    public void TestIsInBounds()
    {
        var grid = new WfcGrid(5, 3, new[] { "grass" });

        AssertBool(grid.IsInBounds(0, 0)).IsTrue();
        AssertBool(grid.IsInBounds(4, 2)).IsTrue();
        AssertBool(grid.IsInBounds(5, 2)).IsFalse();
        AssertBool(grid.IsInBounds(0, 3)).IsFalse();
        AssertBool(grid.IsInBounds(-1, 0)).IsFalse();
    }

    [TestCase]
    public void TestGetNeighborsCorner()
    {
        var grid = new WfcGrid(3, 3, new[] { "grass" });

        var neighbors = grid.GetNeighbors(0, 0).ToList();

        // Top-left corner has only East and South neighbors
        AssertThat(neighbors.Count).IsEqual(2);
        AssertBool(neighbors.Contains(new Vector2I(1, 0))).IsTrue(); // East
        AssertBool(neighbors.Contains(new Vector2I(0, 1))).IsTrue(); // South
    }

    [TestCase]
    public void TestGetNeighborsCenter()
    {
        var grid = new WfcGrid(3, 3, new[] { "grass" });

        var neighbors = grid.GetNeighbors(1, 1).ToList();

        // Center has all 4 neighbors
        AssertThat(neighbors.Count).IsEqual(4);
        AssertBool(neighbors.Contains(new Vector2I(1, 0))).IsTrue(); // North
        AssertBool(neighbors.Contains(new Vector2I(2, 1))).IsTrue(); // East
        AssertBool(neighbors.Contains(new Vector2I(1, 2))).IsTrue(); // South
        AssertBool(neighbors.Contains(new Vector2I(0, 1))).IsTrue(); // West
    }

    [TestCase]
    public void TestIsFullyCollapsedAllCollapsed()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass" });

        // All cells start with only "grass", so they're already collapsed
        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void TestIsFullyCollapsedNotCollapsed()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt" });

        AssertBool(grid.IsFullyCollapsed()).IsFalse();
    }

    [TestCase]
    public void TestHasContradiction()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt" });

        AssertBool(grid.HasContradiction()).IsFalse();

        // Create contradiction by removing all tiles from one cell
        var cell = grid.GetCell(0, 0);
        cell.RemoveTile("grass");
        cell.RemoveTile("dirt");

        AssertBool(grid.HasContradiction()).IsTrue();
    }

    [TestCase]
    public void TestGetCollapsedTileAt()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt" });

        AssertThat(grid.GetCollapsedTileAt(new Vector2I(0, 0))).IsNull();

        grid.GetCell(0, 0).CollapseTo("grass");

        AssertString(grid.GetCollapsedTileAt(new Vector2I(0, 0))).IsEqual("grass");
    }

    [TestCase]
    public void TestClone()
    {
        var grid = new WfcGrid(2, 2, new[] { "grass", "dirt" });
        grid.GetCell(0, 0).CollapseTo("grass");

        var clone = grid.Clone();

        // Clone should have same collapsed state
        AssertString(clone.GetCollapsedTileAt(new Vector2I(0, 0))).IsEqual("grass");

        // Modifying clone shouldn't affect original
        clone.GetCell(1, 1).CollapseTo("dirt");
        AssertBool(grid.GetCell(1, 1).IsCollapsed()).IsFalse();
    }

    [TestCase]
    public void TestGetAllPositions()
    {
        var grid = new WfcGrid(2, 3, new[] { "grass" });

        var positions = grid.GetAllPositions().ToList();

        AssertThat(positions.Count).IsEqual(6);
        AssertBool(positions.Contains(new Vector2I(0, 0))).IsTrue();
        AssertBool(positions.Contains(new Vector2I(1, 2))).IsTrue();
    }
}
