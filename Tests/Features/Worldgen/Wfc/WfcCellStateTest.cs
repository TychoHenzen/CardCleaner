using System;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using GdUnit4;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

[TestSuite]
public class WfcCellStateTest
{
    [TestCase]
    public void TestInitialEntropyMatchesTileCount()
    {
        var tiles = new[] { "grass", "dirt", "sand" };
        var cell = new WfcCellState(tiles);

        AssertThat(cell.GetPossibleTiles().Count).IsEqual(3);
    }

    [TestCase]
    public void TestIsCollapsedWhenOneTileRemains()
    {
        var cell = new WfcCellState(new[] { "grass" });

        AssertBool(cell.IsCollapsed()).IsTrue();
        AssertString(cell.GetCollapsedTile()).IsEqual("grass");
    }

    [TestCase]
    public void TestIsNotCollapsedWithMultipleTiles()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertBool(cell.IsCollapsed()).IsFalse();
    }

    [TestCase]
    public void TestIsContradictionWhenEmpty()
    {
        var cell = new WfcCellState(new[] { "grass" });
        cell.RemoveTile("grass");

        AssertBool(cell.IsContradiction()).IsTrue();
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(0);
    }

    [TestCase]
    public void TestRemoveTileDecreasesEntropy()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand" });

        var removed = cell.RemoveTile("dirt");

        AssertBool(removed).IsTrue();
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(2);
    }

    [TestCase]
    public void TestRemoveNonexistentTileReturnsFalse()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        var removed = cell.RemoveTile("water");

        AssertBool(removed).IsFalse();
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(2);
    }

    [TestCase]
    public void TestContainsTile()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertBool(cell.ContainsTile("grass")).IsTrue();
        AssertBool(cell.ContainsTile("water")).IsFalse();
    }

    [TestCase]
    public void TestCollapseTo()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand" });

        cell.CollapseTo("dirt");

        AssertBool(cell.IsCollapsed()).IsTrue();
        AssertString(cell.GetCollapsedTile()).IsEqual("dirt");
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(1);
    }

    [TestCase]
    public void TestCollapseToInvalidTileThrows()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertThrown(() => cell.CollapseTo("water"))
            .IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void TestGetCollapsedTileThrowsWhenNotCollapsed()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        AssertThrown(() => cell.GetCollapsedTile())
            .IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void TestIntersectWithReducesTiles()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt", "sand", "water" });

        var changed = cell.IntersectWith(new[] { "grass", "water", "stone" });

        AssertBool(changed).IsTrue();
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(2);
        AssertBool(cell.ContainsTile("grass")).IsTrue();
        AssertBool(cell.ContainsTile("water")).IsTrue();
        AssertBool(cell.ContainsTile("dirt")).IsFalse();
    }

    [TestCase]
    public void TestIntersectWithNoChangeReturnsFalse()
    {
        var cell = new WfcCellState(new[] { "grass", "dirt" });

        var changed = cell.IntersectWith(new[] { "grass", "dirt", "sand" });

        AssertBool(changed).IsFalse();
        AssertThat(cell.GetPossibleTiles().Count).IsEqual(2);
    }

    [TestCase]
    public void TestCopyConstructor()
    {
        var original = new WfcCellState(new[] { "grass", "dirt", "sand" });
        original.RemoveTile("dirt");

        var copy = new WfcCellState(original);

        AssertThat(copy.GetPossibleTiles().Count).IsEqual(2);
        AssertBool(copy.ContainsTile("grass")).IsTrue();
        AssertBool(copy.ContainsTile("dirt")).IsFalse();

        // Verify independence
        copy.RemoveTile("grass");
        AssertBool(original.ContainsTile("grass")).IsTrue();
    }
}
