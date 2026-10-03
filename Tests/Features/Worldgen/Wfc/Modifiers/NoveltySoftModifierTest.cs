using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Modifiers;

[TestSuite]
[RequireGodotRuntime]
public class NoveltySoftModifierTest
{
    private NoveltySoftModifier _modifier = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _modifier = new NoveltySoftModifier();
        _grid = new WfcGrid(10, 10, new[] { "grass", "water", "sand" });
    }

    [TestCase]
    public void TestNoNeighborsGetsNoveltyBoost()
    {
        // No neighbors collapsed - should get the novelty boost
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Default novelty boost is 3.0x
        AssertFloat(multiplier).IsEqual(3.0f);
    }

    [TestCase]
    public void TestWithSameTypeNeighborGetsNoBoost()
    {
        // Collapse a grass tile adjacent to position
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Has same-type neighbor, no boost
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestWithDifferentTypeNeighborGetsBoost()
    {
        // Collapse a water tile adjacent to position
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("water");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",  // Asking about grass, neighbor is water
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // No grass neighbors, should get boost
        AssertFloat(multiplier).IsEqual(3.0f);
    }

    [TestCase]
    public void TestCustomNoveltyBoost()
    {
        _modifier.NoveltyBoost = 5.0f;

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        AssertFloat(multiplier).IsEqual(5.0f);
    }

    [TestCase]
    public void TestMultipleSameTypeNeighborsStillNoBoost()
    {
        // Collapse grass tiles on multiple sides
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Has same-type neighbors, no boost (regardless of count)
        AssertFloat(multiplier).IsEqual(1.0f);
    }
}
