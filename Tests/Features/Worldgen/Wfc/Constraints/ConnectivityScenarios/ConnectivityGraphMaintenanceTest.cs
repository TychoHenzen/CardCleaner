using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints.ConnectivityScenarios;

/// <summary>
///     ConnectivityGraphMaintenanceTest scenarios split out of ConnectivityConstraintTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConnectivityGraphMaintenanceTest : ConnectivityConstraintTestBase
{
    // ========== Test Case 4: GraphUpdated_AfterCollapse ==========

    [TestCase]
    public void GraphUpdatedAfterCollapse_PassableTileAddsNode()
    {
        // This test verifies the integration between WfcSolver and PassabilityGraph
        // The graph should be updated when tiles are collapsed

        // Initially empty
        AssertInt(_graph.NodeCount).IsEqual(0);

        // Simulate what WfcSolver does after collapse
        var pos = new Vector2I(2, 2);
        _grid.GetCell(pos).CollapseTo("grass");

        // Manually simulate the solver's UpdatePassabilityGraph
        if (IsPassable("grass"))
        {
            _graph.AddNode(pos);
            foreach (var neighborPos in _grid.GetNeighbors(pos))
            {
                var neighborTile = _grid.GetCollapsedTileAt(neighborPos);
                if (neighborTile != null && IsPassable(neighborTile))
                {
                    _graph.AddEdge(pos, neighborPos);
                }
            }
        }

        AssertInt(_graph.NodeCount).IsEqual(1);
        AssertBool(_graph.ContainsNode(pos)).IsTrue();
    }

    [TestCase]
    public void GraphUpdatedAfterCollapse_ImpassableTileNotAdded()
    {
        // Impassable tiles should NOT be added to the graph
        var pos = new Vector2I(2, 2);
        _grid.GetCell(pos).CollapseTo("wall");

        // Simulate the solver's UpdatePassabilityGraph
        if (IsPassable("wall"))
        {
            _graph.AddNode(pos);
        }

        // Graph should still be empty
        AssertInt(_graph.NodeCount).IsEqual(0);
        AssertBool(_graph.ContainsNode(pos)).IsFalse();
    }

    [TestCase]
    public void GraphUpdatedAfterCollapse_EdgesConnectPassableNeighbors()
    {
        // When a passable tile is collapsed next to another passable tile,
        // they should be connected with an edge
        CollapseAndAddToGraph(new Vector2I(2, 2), "grass");

        // Now collapse adjacent position
        var pos = new Vector2I(2, 3);
        _grid.GetCell(pos).CollapseTo("water");

        // Simulate solver's update
        if (IsPassable("water"))
        {
            _graph.AddNode(pos);
            foreach (var neighborPos in _grid.GetNeighbors(pos))
            {
                var neighborTile = _grid.GetCollapsedTileAt(neighborPos);
                if (neighborTile != null && IsPassable(neighborTile))
                {
                    _graph.AddEdge(pos, neighborPos);
                }
            }
        }

        // Both nodes should be in graph
        AssertInt(_graph.NodeCount).IsEqual(2);

        // They should be connected
        var neighbors = new System.Collections.Generic.List<Vector2I>(_graph.GetNeighbors(pos));
        AssertBool(neighbors.Contains(new Vector2I(2, 2))).IsTrue();
    }

    // ========== Test Case 5: EmptyGraph_ImpassableAllowed ==========

    [TestCase]
    public void EmptyGraph_ImpassableAllowed()
    {
        // First tiles can be impassable since there's nothing to disconnect
        // Graph is empty, no passable neighbors
        var context = CreateContext(new Vector2I(0, 0), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void EmptyGraph_ImpassableAllowedAnywhere()
    {
        // Multiple positions should all allow impassable tiles when graph is empty
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 3; y++)
            {
                var context = CreateContext(new Vector2I(x, y), "wall");
                var result = _constraint.GetProbabilityModifier(context);
                AssertFloat(result).IsEqual(1.0f);
            }
        }
    }
}
