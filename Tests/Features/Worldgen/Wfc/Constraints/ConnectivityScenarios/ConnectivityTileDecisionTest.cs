using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints.ConnectivityScenarios;

/// <summary>
///     ConnectivityTileDecisionTest scenarios split out of ConnectivityConstraintTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConnectivityTileDecisionTest : ConnectivityConstraintTestBase
{
    // ========== Test Case 1: PassableTile_AlwaysReturnsOne ==========

    [TestCase]
    public void PassableTile_AlwaysReturnsOne_EmptyGraph()
    {
        // Even with empty graph, passable tiles should never be banned
        var context = CreateContext(new Vector2I(2, 2), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void PassableTile_AlwaysReturnsOne_WithExistingGraph()
    {
        // Setup: Create a passability graph with some nodes
        SetupLinearGraph();

        var context = CreateContext(new Vector2I(2, 2), "water");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void PassableTile_AlwaysReturnsOne_AtArticulationPoint()
    {
        // Even at an articulation point position, passable tiles should be allowed
        // because they extend connectivity, not break it
        SetupLinearGraph();

        // Position (2,0) is middle of linear graph - would be articulation point
        var context = CreateContext(new Vector2I(2, 0), "grass");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    // ========== Test Case 2: ImpassableTile_NonArticulationPoint_ReturnsOne ==========

    [TestCase]
    public void ImpassableTile_NoPassableNeighbors_ReturnsOne()
    {
        // Position with no passable collapsed neighbors - safe to place wall
        // No collapsed neighbors at all
        var context = CreateContext(new Vector2I(2, 2), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void ImpassableTile_OnePassableNeighbor_ReturnsOne()
    {
        // Only one passable neighbor - can't disconnect anything
        _grid.GetCell(new Vector2I(2, 1)).CollapseTo("grass");
        _graph.AddNode(new Vector2I(2, 1));

        var context = CreateContext(new Vector2I(2, 2), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(1.0f);
    }

    [TestCase]
    public void ImpassableTile_TwoNeighborsAlreadyConnected_ReturnsOne()
    {
        // Two passable neighbors that are already connected without this position
        // Setup: Square shape where removing center isn't breaking connectivity
        //   G
        //  G G   <- all connected via edges, center is just redundant
        //   G
        CollapseAndAddToGraph(new Vector2I(2, 1), "grass"); // top
        CollapseAndAddToGraph(new Vector2I(1, 2), "grass"); // left
        CollapseAndAddToGraph(new Vector2I(3, 2), "grass"); // right
        CollapseAndAddToGraph(new Vector2I(2, 3), "grass"); // bottom

        // Connect them in a ring (so center at 2,2 is not needed for connectivity)
        _graph.AddEdge(new Vector2I(2, 1), new Vector2I(1, 2));
        _graph.AddEdge(new Vector2I(1, 2), new Vector2I(2, 3));
        _graph.AddEdge(new Vector2I(2, 3), new Vector2I(3, 2));
        _graph.AddEdge(new Vector2I(3, 2), new Vector2I(2, 1));

        var context = CreateContext(new Vector2I(2, 2), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        // Position 2,2 would have 4 passable neighbors all connected to each other
        // Not an articulation point since neighbors form a cycle
        AssertFloat(result).IsEqual(1.0f);
    }

    // ========== Test Case 3: ImpassableTile_ArticulationPoint_ReturnsZero ==========

    [TestCase]
    public void ImpassableTile_ArticulationPoint_ReturnsZero_LinearGraph()
    {
        // Linear graph: A -- ? -- B
        // Position ? is an articulation point - would disconnect A from B
        CollapseAndAddToGraph(new Vector2I(1, 0), "grass"); // A
        CollapseAndAddToGraph(new Vector2I(3, 0), "grass"); // B

        // They are not directly connected - only through position (2,0)
        // No edge between them

        var context = CreateContext(new Vector2I(2, 0), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        AssertFloat(result).IsEqual(0.0f);
    }

    [TestCase]
    public void ImpassableTile_ArticulationPoint_ReturnsZero_BridgeNode()
    {
        // Setup: Two clusters connected only through a bridge position
        //
        //   A2 - A   ?   C - C2
        //       (1,1) (2,1) (3,1)
        //
        // Position (2,1) has neighbors (1,1)=A and (3,1)=C
        // A and C are not directly connected - only through position (2,1)
        CollapseAndAddToGraph(new Vector2I(1, 1), "grass"); // A
        CollapseAndAddToGraph(new Vector2I(0, 1), "grass"); // A2 (extends cluster 1)
        CollapseAndAddToGraph(new Vector2I(3, 1), "grass"); // C
        CollapseAndAddToGraph(new Vector2I(4, 1), "grass"); // C2 (extends cluster 2)

        // Connect within clusters (A-A2 and C-C2)
        _graph.AddEdge(new Vector2I(1, 1), new Vector2I(0, 1));
        _graph.AddEdge(new Vector2I(3, 1), new Vector2I(4, 1));

        // Bridge position (2,1) - neighbors include A at (1,1) and C at (3,1)
        var context = CreateContext(new Vector2I(2, 1), "wall");

        var result = _constraint.GetProbabilityModifier(context);

        // Position (2,1) connects the two clusters
        // Making it impassable would disconnect them
        AssertFloat(result).IsEqual(0.0f);
    }

    private void SetupLinearGraph()
    {
        // Setup: A -- B -- C at y=0
        // A at (1,0), B at (2,0), C at (3,0)
        CollapseAndAddToGraph(new Vector2I(1, 0), "grass");
        CollapseAndAddToGraph(new Vector2I(2, 0), "grass");
        CollapseAndAddToGraph(new Vector2I(3, 0), "grass");

        _graph.AddEdge(new Vector2I(1, 0), new Vector2I(2, 0));
        _graph.AddEdge(new Vector2I(2, 0), new Vector2I(3, 0));
    }
}
