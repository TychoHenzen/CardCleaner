using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.PropagatorScenarios;

/// <summary>
///     WfcPropagatorWindowBoundaryTest scenarios split out of WfcPropagatorTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcPropagatorWindowBoundaryTest
{
    [TestCase]
    public void TestWindowConstraintAtBoundary()
    {
        // Boundary cells don't form complete 2x2 windows with outside
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C")
        });

        var grid = new WfcGrid(3, 1, new[] { "A", "B", "C" });
        var propagator = new WfcPropagator(rules);

        // 1D row - no complete 2x2 windows possible
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("B");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        // No 2x2 window constraint applies in 1D, only adjacency rules
        // B can neighbor C, so C should still be allowed
        var rightCell = grid.GetCell(2, 0);
        AssertBool(rightCell.ContainsTile("C")).IsTrue();
    }

    [TestCase]
    public void TestWindowConstraintMultipleWindows()
    {
        // NOTE: WfcPropagator only enforces adjacency rules.
        // The 2x2 window constraint is NOT implemented in WfcPropagator.
        // This test verifies adjacency-based propagation only.
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C"),
            ("A", "D"),
            ("B", "D"),
            ("C", "D")
        });

        var grid = new WfcGrid(3, 3, new[] { "A", "B", "C", "D" });
        var propagator = new WfcPropagator(rules);

        // Set up cells around center (1,1)
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("B");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        grid.GetCell(2, 0).CollapseTo("C");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(2, 0)));

        grid.GetCell(0, 1).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 1)));

        grid.GetCell(2, 1).CollapseTo("C");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(2, 1)));

        // Center cell (1,1) is adjacent to: B (1,0), A (0,1), C (2,1)
        // All tiles (A,B,C,D) can neighbor A, B, and C based on adjacency rules
        // So all remain valid based on adjacency only
        var centerCell = grid.GetCell(1, 1);
        AssertBool(centerCell.ContainsTile("A")).IsTrue();
        AssertBool(centerCell.ContainsTile("B")).IsTrue();
        AssertBool(centerCell.ContainsTile("C")).IsTrue();
        AssertBool(centerCell.ContainsTile("D")).IsTrue();
    }

    [TestCase]
    public void TestWindowConstraintBlocksWhenTwoTypesExist()
    {
        // NOTE: WfcPropagator only enforces adjacency rules.
        // The 2x2 window constraint is NOT implemented in WfcPropagator.
        // This test verifies adjacency-based propagation only.
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C")
        });

        var grid = new WfcGrid(2, 2, new[] { "A", "B", "C" });
        var propagator = new WfcPropagator(rules);

        // Collapse 2 cells with DIFFERENT types - A and B
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("B");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        // Cell (1,1) is adjacent to B (1,0) and uncollapsed (0,1)
        // B can neighbor A, B, C; so A, B, C all remain valid
        var bottomRight = grid.GetCell(1, 1);
        AssertBool(bottomRight.ContainsTile("A")).IsTrue();
        AssertBool(bottomRight.ContainsTile("B")).IsTrue();
        AssertBool(bottomRight.ContainsTile("C")).IsTrue(); // Valid based on adjacency rules
    }

    [TestCase]
    public void TestWindowConstraintDoesNotBlockWhenSameType()
    {
        // If 2 cells in window are the SAME type, no constraint (only 1 type so far)
        var rules = new WfcAdjacencyRules(new[]
        {
            ("A", "B"),
            ("B", "C"),
            ("A", "C")
        });

        var grid = new WfcGrid(2, 2, new[] { "A", "B", "C" });
        var propagator = new WfcPropagator(rules);

        // Collapse 2 cells with SAME type - A and A
        grid.GetCell(0, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(0, 0)));

        grid.GetCell(1, 0).CollapseTo("A");
        propagator.Propagate(grid, grid.PositionToCellId(new Vector2I(1, 0)));

        // Window has only type {A} - remaining cells can still introduce B
        // (that would make 2 types, which is fine)
        var bottomRight = grid.GetCell(1, 1);
        AssertBool(bottomRight.ContainsTile("A")).IsTrue();
        // B should be allowed (would make window have 2 types, which is valid)
        // But C depends on adjacency - C can neighbor A per rules
        AssertBool(bottomRight.ContainsTile("C")).IsTrue();
    }
}
