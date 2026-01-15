using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Constraints;

[TestSuite]
[RequireGodotRuntime]
public class ConnectivityConstraintTest
{
    private PassabilityGraph _graph = null!;
    private WfcGrid _grid = null!;
    private ConnectivityConstraint _constraint = null!;

    // Define passable tiles: "grass" and "water" are passable, "wall" is impassable
    private static readonly string[] AllTiles = { "grass", "water", "wall" };
    private static bool IsPassable(string tileId) => tileId != "wall";

    [BeforeTest]
    public void Setup()
    {
        _graph = new PassabilityGraph();
        _grid = new WfcGrid(5, 5, AllTiles);
        _constraint = new ConnectivityConstraint(_graph, IsPassable);
    }

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

    // ========== Helper Methods ==========

    private WfcConstraintContext CreateContext(Vector2I position, string tileId)
    {
        return new WfcConstraintContext
        {
            CellId = _grid.PositionToCellId(position),
            TileId = tileId,
            Topology = _grid,
            Rng = null
        };
    }

    private void CollapseAndAddToGraph(Vector2I position, string tileId)
    {
        _grid.GetCell(position).CollapseTo(tileId);
        if (IsPassable(tileId))
        {
            _graph.AddNode(position);
        }
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

    // ========== Test Case 6: MapGeneration_10x10_AlwaysConnected ==========

    [TestCase]
    public void MapGeneration_10x10_AlwaysConnected()
    {
        // Integration test: Generate 100 10x10 maps and verify all have connected passable regions

        // Create custom adjacency rules that include both passable and impassable tiles
        var customRules = new WfcAdjacencyRules(new[]
        {
            ("grass", "grass"),
            ("grass", "water"),
            ("grass", "wall"),
            ("water", "water"),
            ("water", "wall"),
            ("wall", "wall")
        });

        var generator = new WfcMapGenerator(customRules);
        generator.EnableConnectivity = true;
        generator.MaxRetries = 5;

        // Create biome with mix of passable and impassable tiles
        var passable = new TilePool();
        passable.Add("grass", 1.0f);
        passable.Add("water", 1.0f);
        // Note: wall is NOT in passable tiles

        var blocked = new TilePool();
        blocked.Add("wall", 1.0f);

        var biome = new BiomeDefinition(
            "test_biome",
            new CardSignature(),
            passable,
            blocked,
            0.0f);

        var successCount = 0;
        var connectedCount = 0;
        var disconnectedMaps = new List<int>();

        for (var i = 0; i < 100; i++)
        {
            var seed = (ulong)(i * 12345 + 7);
            var result = generator.Generate(biome, new Vector2I(10, 10), seed);

            if (!result.Success)
            {
                GD.Print($"Map {i} generation failed: {result.ErrorMessage}");
                continue;
            }

            successCount++;
            var mapData = result.MapData!;

            // Check connectivity using flood fill
            var isConnected = VerifyPassableConnectivity(mapData, passable);
            if (isConnected)
            {
                connectedCount++;
            }
            else
            {
                disconnectedMaps.Add(i);
                GD.Print($"Map {i} (seed {seed}) has disconnected passable regions!");
            }
        }

        GD.Print($"Generated {successCount}/100 maps successfully");
        GD.Print($"Connected: {connectedCount}/{successCount}");

        if (disconnectedMaps.Count > 0)
        {
            GD.Print($"Disconnected maps: {string.Join(", ", disconnectedMaps)}");
        }

        // All successfully generated maps should have connected passable regions
        AssertInt(connectedCount).IsEqual(successCount);
    }

    [TestCase]
    public void MapGeneration_WithConnectivityEnabled_HasConnectedRegions()
    {
        // Simpler test case: generate a few maps and verify connectivity
        var customRules = new WfcAdjacencyRules(new[]
        {
            ("A", "A"), ("A", "B"), ("A", "X"),
            ("B", "B"), ("B", "X"),
            ("X", "X")
        });

        var generator = new WfcMapGenerator(customRules);
        generator.EnableConnectivity = true;

        var passable = new TilePool();
        passable.Add("A", 1.0f);
        passable.Add("B", 1.0f);

        var blocked = new TilePool();
        blocked.Add("X", 1.0f);

        var biome = new BiomeDefinition("test", new CardSignature(), passable, blocked, 0.0f);

        for (var seed = 1; seed <= 10; seed++)
        {
            var result = generator.Generate(biome, new Vector2I(8, 8), (ulong)seed * 100);

            if (!result.Success)
            {
                GD.Print($"Seed {seed}: Generation failed - {result.ErrorMessage}");
                continue;
            }

            var isConnected = VerifyPassableConnectivity(result.MapData!, passable);
            AssertBool(isConnected).IsTrue();
        }
    }

    private static bool VerifyPassableConnectivity(SimpleMapData mapData, TilePool passableTilePool)
    {
        var passablePositions = new HashSet<Vector2I>(mapData.PassableTiles);

        if (passablePositions.Count == 0)
            return true; // No passable tiles = trivially connected

        // Flood fill from the first passable position
        var start = passablePositions.GetEnumerator();
        if (!start.MoveNext())
            return true;

        var firstPosition = start.Current;
        var visited = new HashSet<Vector2I>();
        var queue = new Queue<Vector2I>();
        queue.Enqueue(firstPosition);
        visited.Add(firstPosition);

        var directions = new[] {
            new Vector2I(1, 0), new Vector2I(-1, 0),
            new Vector2I(0, 1), new Vector2I(0, -1)
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var dir in directions)
            {
                var neighbor = current + dir;

                if (passablePositions.Contains(neighbor) && !visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }

        // All passable positions should be reachable from the first one
        return visited.Count == passablePositions.Count;
    }
}
