using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Connectivity;

[TestSuite]
[RequireGodotRuntime]
public class PassabilityGraphTest
{
    private PassabilityGraph _graph = null!;

    [BeforeTest]
    public void Setup()
    {
        _graph = new PassabilityGraph();
    }

    // ========== Graph Mutation Tests (ST004) ==========

    [TestCase]
    public void AddNode_UpdatesGraph()
    {
        var pos = new Vector2I(2, 3);

        _graph.AddNode(pos);

        AssertInt(_graph.NodeCount).IsEqual(1);
        AssertBool(_graph.ContainsNode(pos)).IsTrue();
    }

    [TestCase]
    public void AddNode_Idempotent_DoesNotDuplicate()
    {
        var pos = new Vector2I(2, 3);

        _graph.AddNode(pos);
        _graph.AddNode(pos);

        AssertInt(_graph.NodeCount).IsEqual(1);
    }

    [TestCase]
    public void RemoveNode_UpdatesGraph()
    {
        var pos1 = new Vector2I(0, 0);
        var pos2 = new Vector2I(1, 0);
        _graph.AddEdge(pos1, pos2);

        _graph.RemoveNode(pos1);

        AssertInt(_graph.NodeCount).IsEqual(1);
        AssertBool(_graph.ContainsNode(pos1)).IsFalse();
        AssertBool(_graph.ContainsNode(pos2)).IsTrue();
        // Verify edge is cleaned up - pos2 should have no neighbors
        AssertBool(_graph.GetNeighbors(pos2).GetEnumerator().MoveNext()).IsFalse();
    }

    [TestCase]
    public void RemoveNode_NonExistent_NoOp()
    {
        _graph.AddNode(new Vector2I(0, 0));

        _graph.RemoveNode(new Vector2I(99, 99));

        AssertInt(_graph.NodeCount).IsEqual(1);
    }

    [TestCase]
    public void AddEdge_ConnectsNodes()
    {
        var a = new Vector2I(0, 0);
        var b = new Vector2I(1, 0);

        _graph.AddEdge(a, b);

        AssertInt(_graph.NodeCount).IsEqual(2);
        AssertBool(_graph.ContainsNode(a)).IsTrue();
        AssertBool(_graph.ContainsNode(b)).IsTrue();

        // Verify bidirectional connection
        var neighborsA = new System.Collections.Generic.List<Vector2I>(_graph.GetNeighbors(a));
        var neighborsB = new System.Collections.Generic.List<Vector2I>(_graph.GetNeighbors(b));

        AssertInt(neighborsA.Count).IsEqual(1);
        AssertBool(neighborsA.Contains(b)).IsTrue();
        AssertInt(neighborsB.Count).IsEqual(1);
        AssertBool(neighborsB.Contains(a)).IsTrue();
    }

    [TestCase]
    public void AddEdge_ImplicitlyAddsNodes()
    {
        var a = new Vector2I(5, 5);
        var b = new Vector2I(6, 5);

        _graph.AddEdge(a, b);

        AssertBool(_graph.ContainsNode(a)).IsTrue();
        AssertBool(_graph.ContainsNode(b)).IsTrue();
    }

    // ========== Edge Case Tests (ST007) ==========

    [TestCase]
    public void EmptyGraph_IsArticulationPoint_ReturnsFalse()
    {
        // Empty graph - no nodes exist
        var result = _graph.IsArticulationPoint(new Vector2I(0, 0));

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void SingleNode_IsArticulationPoint_ReturnsFalse()
    {
        var pos = new Vector2I(5, 5);
        _graph.AddNode(pos);

        var result = _graph.IsArticulationPoint(pos);

        AssertBool(result).IsFalse();
    }

    // ========== Simple Graph Structure Tests (ST008) ==========

    [TestCase]
    public void TwoNodes_EitherIsArticulationPoint()
    {
        // Graph: A -- B
        // Removing either node disconnects (leaves other isolated)
        var a = new Vector2I(0, 0);
        var b = new Vector2I(1, 0);
        _graph.AddEdge(a, b);

        AssertBool(_graph.IsArticulationPoint(a)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(b)).IsTrue();
    }

    [TestCase]
    public void LinearPath_MiddleIsArticulationPoint()
    {
        // Graph: A -- B -- C
        // Only B is an articulation point
        var a = new Vector2I(0, 0);
        var b = new Vector2I(1, 0);
        var c = new Vector2I(2, 0);

        _graph.AddEdge(a, b);
        _graph.AddEdge(b, c);

        AssertBool(_graph.IsArticulationPoint(a)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(b)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(c)).IsFalse();
    }

    [TestCase]
    public void Square_NoArticulationPoints()
    {
        // Graph: 2x2 cycle
        //   A -- B
        //   |    |
        //   C -- D
        // No articulation points (any node can be removed and graph stays connected)
        var a = new Vector2I(0, 0);
        var b = new Vector2I(1, 0);
        var c = new Vector2I(0, 1);
        var d = new Vector2I(1, 1);

        _graph.AddEdge(a, b);
        _graph.AddEdge(b, d);
        _graph.AddEdge(d, c);
        _graph.AddEdge(c, a);

        AssertBool(_graph.IsArticulationPoint(a)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(b)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(c)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(d)).IsFalse();
    }

    // ========== Bridge Node Test (ST009) ==========

    [TestCase]
    public void BridgeNode_IsArticulationPoint()
    {
        // Graph: Two triangles connected by a bridge node
        //
        //   A          E
        //  / \        / \
        // B---C -- D --F---G
        //
        // C and D are articulation points (they form the bridge)
        var a = new Vector2I(0, 0);
        var b = new Vector2I(0, 1);
        var c = new Vector2I(1, 1);
        var d = new Vector2I(2, 1);
        var e = new Vector2I(3, 0);
        var f = new Vector2I(3, 1);
        var g = new Vector2I(4, 1);

        // Left triangle (A-B-C)
        _graph.AddEdge(a, b);
        _graph.AddEdge(b, c);
        _graph.AddEdge(c, a);

        // Right triangle (E-F-G)
        _graph.AddEdge(e, f);
        _graph.AddEdge(f, g);
        _graph.AddEdge(g, e);

        // Bridge (C-D-F)
        _graph.AddEdge(c, d);
        _graph.AddEdge(d, f);

        // Bridge nodes are articulation points
        AssertBool(_graph.IsArticulationPoint(c)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(d)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(f)).IsTrue();

        // Non-bridge nodes in triangles are not articulation points
        AssertBool(_graph.IsArticulationPoint(a)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(b)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(e)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(g)).IsFalse();
    }

    [TestCase]
    public void NodeNotInGraph_IsArticulationPoint_ReturnsFalse()
    {
        _graph.AddNode(new Vector2I(0, 0));

        var result = _graph.IsArticulationPoint(new Vector2I(99, 99));

        AssertBool(result).IsFalse();
    }

    [TestCase]
    public void LongerChain_OnlyMiddleNodesAreArticulationPoints()
    {
        // Graph: A -- B -- C -- D -- E
        // B, C, D are articulation points; A and E are not
        var a = new Vector2I(0, 0);
        var b = new Vector2I(1, 0);
        var c = new Vector2I(2, 0);
        var d = new Vector2I(3, 0);
        var e = new Vector2I(4, 0);

        _graph.AddEdge(a, b);
        _graph.AddEdge(b, c);
        _graph.AddEdge(c, d);
        _graph.AddEdge(d, e);

        AssertBool(_graph.IsArticulationPoint(a)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(b)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(c)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(d)).IsTrue();
        AssertBool(_graph.IsArticulationPoint(e)).IsFalse();
    }

    [TestCase]
    public void StarGraph_CenterIsArticulationPoint()
    {
        // Graph: Star with center C and leaves A, B, D, E
        //     A
        //     |
        // B - C - D
        //     |
        //     E
        var center = new Vector2I(2, 2);
        var a = new Vector2I(2, 1);
        var b = new Vector2I(1, 2);
        var d = new Vector2I(3, 2);
        var e = new Vector2I(2, 3);

        _graph.AddEdge(center, a);
        _graph.AddEdge(center, b);
        _graph.AddEdge(center, d);
        _graph.AddEdge(center, e);

        // Center is articulation point (root with 4 children)
        AssertBool(_graph.IsArticulationPoint(center)).IsTrue();

        // Leaves are not articulation points
        AssertBool(_graph.IsArticulationPoint(a)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(b)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(d)).IsFalse();
        AssertBool(_graph.IsArticulationPoint(e)).IsFalse();
    }
}
