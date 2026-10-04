using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints.ConnectivityScenarios;

/// <summary>
///     Shared fixture for the ConnectivityConstraintTest scenario suites.
/// </summary>
public abstract class ConnectivityConstraintTestBase
{
    protected PassabilityGraph _graph = null!;

    protected WfcGrid _grid = null!;

    protected ConnectivityConstraint _constraint = null!;

    // Define passable tiles: "grass" and "water" are passable, "wall" is impassable
    protected static readonly string[] AllTiles = { "grass", "water", "wall" };

    [BeforeTest]
    public void Setup()
    {
        _graph = new PassabilityGraph();
        _grid = new WfcGrid(5, 5, AllTiles);
        _constraint = new ConnectivityConstraint(_graph, IsPassable);
    }

    protected static bool IsPassable(string tileId) => tileId != "wall";

    // ========== Helper Methods ==========

    protected WfcConstraintContext CreateContext(Vector2I position, string tileId)
    {
        return new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(position),
            TileId = tileId,
            Topology = _grid,
            Rng = null
        };
    }

    protected void CollapseAndAddToGraph(Vector2I position, string tileId)
    {
        _grid.GetCell(position).CollapseTo(tileId);
        if (IsPassable(tileId))
        {
            _graph.AddNode(position);
        }
    }
}
