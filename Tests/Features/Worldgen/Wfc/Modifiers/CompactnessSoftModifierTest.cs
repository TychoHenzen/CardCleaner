using CardCleaner.Scripts.Features.Worldgen.Wfc;
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
        // No neighbors - handled by NoveltySoftModifier, this should be neutral
        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestOneNeighborGetsSnakePenalty()
    {
        // One same-type neighbor - this is a snake extension
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Default snake penalty is 0.3x
        AssertFloat(multiplier).IsEqual(0.3f);
    }

    [TestCase]
    public void TestTwoNeighborsIsNeutral()
    {
        // Two same-type neighbors - corner or line, neutral
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        AssertFloat(multiplier).IsEqual(1.0f);
    }

    [TestCase]
    public void TestThreeNeighborsGetsCompactBoost()
    {
        // Three same-type neighbors - filling in a compact area
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Default compact boost is 1.5x
        AssertFloat(multiplier).IsEqual(1.5f);
    }

    [TestCase]
    public void TestFourNeighborsGetsCompactBoost()
    {
        // Four same-type neighbors - completely surrounded, filling last gap
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(5, 6)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Default compact boost is 1.5x
        AssertFloat(multiplier).IsEqual(1.5f);
    }

    [TestCase]
    public void TestOnlyCountsSameType()
    {
        // Mix of grass and water neighbors, only 1 grass
        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("water");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("water");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        // Only 1 grass neighbor = snake penalty
        AssertFloat(multiplier).IsEqual(0.3f);
    }

    [TestCase]
    public void TestCustomSnakePenalty()
    {
        _modifier.SnakePenalty = 0.1f;

        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        AssertFloat(multiplier).IsEqual(0.1f);
    }

    [TestCase]
    public void TestCustomCompactBoost()
    {
        _modifier.CompactBoost = 2.0f;

        _grid.GetCell(new Vector2I(5, 4)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(4, 5)).CollapseTo("grass");
        _grid.GetCell(new Vector2I(6, 5)).CollapseTo("grass");

        var context = new SoftModifierContext
        {
            Position = new Vector2I(5, 5),
            TileId = "grass",
            Grid = _grid
        };

        var multiplier = _modifier.CalculateMultiplier(context);

        AssertFloat(multiplier).IsEqual(2.0f);
    }
}
