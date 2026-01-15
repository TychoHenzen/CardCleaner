using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints;

[TestSuite]
public class WfcConstraintContextTest
{
    [TestCase]
    [RequireGodotRuntime]
    public void WfcConstraintContext_InitializesCorrectly()
    {
        // Context struct should hold all required properties
        var grid = new WfcGrid(5, 5, new[] { "grass", "dirt" });
        var rng = new RandomNumberGenerator { Seed = 42 };

        var context = new WfcConstraintContext
        {
            CellId = grid.PositionToCellId(new Vector2I(2, 3)),
            TileId = "grass",
            Topology = grid,
            Rng = rng
        };

        AssertThat(context.Position).IsEqual(new Vector2I(2, 3));
        AssertThat(context.TileId).IsEqual("grass");
        AssertThat(context.Grid).IsNotNull();
        AssertThat(context.Grid).IsSame(grid);
        AssertThat(context.Rng).IsNotNull();
        AssertThat(context.Rng).IsSame(rng);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void WfcConstraintContext_DefaultRngIsNull()
    {
        // Optional RNG property defaults correctly to null
        var grid = new WfcGrid(3, 3, new[] { "grass" });

        var context = new WfcConstraintContext
        {
            CellId = grid.PositionToCellId(new Vector2I(1, 1)),
            TileId = "grass",
            Topology = grid
            // Rng not specified
        };

        AssertThat(context.Rng).IsNull();
    }
}
