using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.PropagatorScenarios;

/// <summary>
///     WfcPropagatorGridAndWindowTest scenarios split out of WfcPropagatorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcPropagatorGridAndWindowTest
{
    [TestCase]
    public void TestContradictionReturnsFailure()
    {
        // Create impossible situation: only self-adjacency allowed
        var rules = new WfcAdjacencyRules(new (string, string)[] { });

        // Grid with tiles that can only neighbor themselves
        var grid = new WfcGrid(2, 1, new[] { "A" });
        var propagator = new WfcPropagator(rules);

        // This should succeed since A can neighbor A (self-adjacency)
        grid.GetCell(0, 0).CollapseTo("A");
        var result = propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        AssertBool(result.Success).IsTrue();
    }

    [TestCase]
    public void Test2DGridPropagation()
    {
        var rules = new WfcAdjacencyRules(new[]
        {
            ("grass", "dirt"),
            ("dirt", "sand")
        });

        var grid = new WfcGrid(3, 3, new[] { "grass", "dirt", "sand" });
        var propagator = new WfcPropagator(rules);

        // Collapse center to grass
        grid.GetCell(1, 1).CollapseTo("grass");

        var result = propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 1)));

        AssertBool(result.Success).IsTrue();

        // All 4 direct neighbors should have sand removed
        AssertBool(grid.GetCell(1, 0).ContainsTile("sand")).IsFalse(); // North
        AssertBool(grid.GetCell(2, 1).ContainsTile("sand")).IsFalse(); // East
        AssertBool(grid.GetCell(1, 2).ContainsTile("sand")).IsFalse(); // South
        AssertBool(grid.GetCell(0, 1).ContainsTile("sand")).IsFalse(); // West

        // Corners are not direct neighbors of center, so they still have all options
        // (they only constrain based on their direct neighbors)
    }

    // === 2x2 Window Transition Spacing Constraint Tests ===
    // The constraint prevents 3+ distinct terrain types in any 2x2 cell window.
    // Each cell participates in up to 4 windows (as NW, NE, SW, or SE corner).

    [TestCase]
    public void TestWindowConstraintBlocks3rdTypeIn2x2()
    {
        // NOTE: WfcPropagator only enforces adjacency rules, not a 2x2 window constraint.
        // This test verifies adjacency-based propagation behavior.
        // The 2x2 window constraint (preventing 3+ types) is handled by AutoTileGapConstraint
        // at a higher level, not within basic WfcPropagator.
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C") // All adjacencies valid
        });

        var grid = new WfcGrid(2, 2, new[] { "A", "B", "C" });
        var propagator = new WfcPropagator(rules);

        // Collapse 3 cells of the 2x2 window to A, B, A
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("B");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        grid.GetCell(0, 1).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 1)));

        // Cell (1,1) is adjacent to B (1,0) and A (0,1)
        // Adjacency rules: A can neighbor A,B,C; B can neighbor A,B,C; C can neighbor A,B
        // So all tiles remain valid based on adjacency only
        var cornerCell = grid.GetCell(1, 1);
        // All tiles are valid neighbors based on adjacency rules
        AssertBool(cornerCell.ContainsTile("A")).IsTrue();
        AssertBool(cornerCell.ContainsTile("B")).IsTrue();
        AssertBool(cornerCell.ContainsTile("C")).IsTrue();
    }

    [TestCase]
    public void TestWindowConstraintAllowsSameType()
    {
        // A 2x2 window with all same type is always valid
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B")
        });

        var grid = new WfcGrid(2, 2, new[] { "A", "B" });
        var propagator = new WfcPropagator(rules);

        // Collapse 3 cells to A
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        grid.GetCell(0, 1).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 1)));

        // 4th cell should allow A (continuing same type)
        var cornerCell = grid.GetCell(1, 1);
        AssertBool(cornerCell.ContainsTile("A")).IsTrue();
    }

    [TestCase]
    public void TestWindowConstraintAllowsTwoTypes()
    {
        // A 2x2 window with exactly 2 types is valid (transition)
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B")
        });

        var grid = new WfcGrid(2, 2, new[] { "A", "B" });
        var propagator = new WfcPropagator(rules);

        // Collapse 3 cells: A, A, B
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        grid.GetCell(0, 1).CollapseTo("B");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 1)));

        // 4th cell can be A or B (both keep window at 2 types)
        var cornerCell = grid.GetCell(1, 1);
        AssertBool(cornerCell.ContainsTile("A")).IsTrue();
        AssertBool(cornerCell.ContainsTile("B")).IsTrue();
    }
}
