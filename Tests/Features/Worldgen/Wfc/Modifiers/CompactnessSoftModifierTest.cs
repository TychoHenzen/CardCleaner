using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Modifiers;

[TestSuite]
[RequireGodotRuntime]
public class CompactnessSoftModifierTest
{
    private CompactnessSoftModifier _modifier = null!;
    private WfcGrid _grid = null!;

    [BeforeTest]
    public void Setup()
    {
        _modifier = new CompactnessSoftModifier();
        _grid = new WfcGrid(10, 10, new[] { "grass", "water", "sand" });
    }

    [TestCase]
    public void TestNoNeighborsIsNeutral()
    {
        // No neighbors - neutral (no match)
        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestOneNeighborIsNeutral()
    {
        // One same-type neighbor - normal edge extension, neutral (no penalty!)
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // One neighbor = neutral (don't penalize normal region growth)
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestTwoNeighborsGetsCornerBoost()
    {
        // Two same-type neighbors - corner fill, small boost
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Default corner boost is 1.3x
        AssertFloat(multiplier).IsEqual(1.3f);
    }

    [TestCase]
    public void TestThreeNeighborsGetsGapFillBoost()
    {
        // Three same-type neighbors - filling in a gap
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

        // Default gap fill boost is 2.0x
        AssertFloat(multiplier).IsEqual(2.0f);
    }

    [TestCase]
    public void TestFourNeighborsGetsGapFillBoost()
    {
        // Four same-type neighbors - completely surrounded, filling last gap
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(5, 6)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Default gap fill boost is 2.0x
        AssertFloat(multiplier).IsEqual(2.0f);
    }

    [TestCase]
    public void TestOnlyCountsSameType()
    {
        // Mix of grass and water neighbors, only 1 grass
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("water");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("water");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        // Only 1 grass neighbor = neutral (normal edge growth)
        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestCustomCornerBoost()
    {
        _modifier.CornerBoost = 1.5f;

        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");

        var context = new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(new Vector2I(5, 5)),
            TileId = "grass",
            Topology = _grid
        };

        var multiplier = _modifier.GetProbabilityModifier(context);

        AssertFloat(multiplier).IsEqual(1.5f);
    }

    [TestCase]
    public void TestCustomGapFillBoost()
    {
        _modifier.GapFillBoost = 3.0f;

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

        AssertFloat(multiplier).IsEqual(3.0f);
    }
}
