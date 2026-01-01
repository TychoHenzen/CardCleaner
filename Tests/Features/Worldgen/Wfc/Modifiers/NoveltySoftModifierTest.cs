using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Modifiers;

[TestSuite]
[RequireGodotRuntime]
public class NoveltySoftModifierTest
{
    private BlobSizeTracker _tracker = null!;
    private NoveltySoftModifier _modifier = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _tracker = new BlobSizeTracker();
        _modifier = new NoveltySoftModifier(_tracker);
        _grid = new WfcGrid(10, 10, new[] { "grass", "water", "sand" });
    }

    [TestCase]
    public void TestNoNeighborsGetsNoveltyBoost()
    {
        // No neighbors collapsed - should get the novelty boost
        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Default novelty boost is 3.0x
        AssertFloat(multiplier).IsEqual(3.0f);
    }

    [TestCase]
    public void TestWithSameTypeNeighborGetsNoBoost()
    {
        // Collapse a grass tile adjacent to position
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _tracker.RegisterCollapse(new Vector2I(5, 4), "grass", _grid);

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Has same-type neighbor, no boost
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestWithDifferentTypeNeighborGetsBoost()
    {
        // Collapse a water tile adjacent to position
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("water");
        _tracker.RegisterCollapse(new Vector2I(5, 4), "water", _grid);

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",  // Asking about grass, neighbor is water
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // No grass neighbors, should get boost
        AssertFloat(multiplier).IsEqual(3.0f);
    }

    [TestCase]
    public void TestCustomNoveltyBoost()
    {
        _modifier.NoveltyBoost = 5.0f;

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        AssertFloat(multiplier).IsEqual(5.0f);
    }

    [TestCase]
    public void TestMultipleSameTypeNeighborsStillNoBoost()
    {
        // Collapse grass tiles on multiple sides
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");
        _tracker.RegisterCollapse(new Vector2I(5, 4), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(4, 5), "grass", _grid);
        _tracker.RegisterCollapse(new Vector2I(6, 5), "grass", _grid);

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Has same-type neighbors, no boost (regardless of count)
        AssertFloat(multiplier).IsEqual(1.0f);
    }
}
