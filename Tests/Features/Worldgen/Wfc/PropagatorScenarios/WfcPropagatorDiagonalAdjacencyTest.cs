using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.PropagatorScenarios;

/// <summary>
///     Adjacency rules apply to edge-sharing cells only; diagonal cells share no edge.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcPropagatorDiagonalAdjacencyTest
{
    private static readonly string[] Tiles = { "A", "B", "C" };

    private WfcPropagator _propagator = null!;
    private WfcGrid _grid3 = null!;
    private WfcGrid _grid2 = null!;

    [BeforeTest]
    public void Setup()
    {
        _propagator = new WfcPropagator(new WfcAdjacencyRules(new[] { ("A", "B"), ("B", "C") }));
        _grid3 = new WfcGrid(3, 3, Tiles);
        _grid2 = new WfcGrid(2, 2, Tiles);
    }

    [TestCase]
    public void DiagonalIncompatibleTile_IsNotRemoved()
    {
        _grid3.GetCell(0, 0).CollapseTo("A");
        var result = _propagator.Propagate(_grid3, _grid3.PositionToCellId(new Vector2I(0, 0)));

        AssertBool(result.Success).IsTrue();
        // (1,1) touches (0,0) only at a corner, so A-C incompatibility must not apply.
        AssertBool(_grid3.GetCell(1, 1).ContainsTile("C")).IsTrue();
        // (1,0) shares an edge with (0,0), so C is removed there.
        AssertBool(_grid3.GetCell(1, 0).ContainsTile("C")).IsFalse();
    }

    [TestCase]
    public void DiagonalIncompatiblePlacement_DoesNotContradict()
    {
        _grid2.GetCell(0, 0).CollapseTo("A");
        _propagator.Propagate(_grid2, _grid2.PositionToCellId(new Vector2I(0, 0)));
        _grid2.GetCell(1, 1).CollapseTo("C");
        var result = _propagator.Propagate(_grid2, _grid2.PositionToCellId(new Vector2I(1, 1)));

        AssertBool(result.Success).IsTrue();
        AssertBool(_grid2.HasContradiction()).IsFalse();
    }

    [TestCase]
    public void WindowNeighborhood_StillIncludesDiagonalCells()
    {
        // The window neighborhood stays 8-way for re-evaluation and the auto-tile gap rule.
        IWfcTopology topology = _grid3;
        var center = _grid3.PositionToCellId(new Vector2I(1, 1));

        AssertInt(topology.GetWindowNeighbors(center).Count()).IsEqual(8);
        AssertInt(topology.GetNeighbors(center).Count()).IsEqual(4);
    }
}
