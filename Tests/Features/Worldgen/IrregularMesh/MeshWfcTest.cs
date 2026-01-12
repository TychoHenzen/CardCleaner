using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using IrregularMeshNs = CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh;

/// <summary>
/// Tests for MeshWfcGrid and MeshWfcSolver.
/// Verifies WFC terrain generation on irregular mesh.
/// </summary>
[TestSuite]
public class MeshWfcTest
{
    private IrregularMeshNs.IrregularMesh _testMesh = null!;

    [BeforeTest]
    public void Setup()
    {
        _testMesh = CreateSimpleTestMesh();
    }

    #region MeshWfcGrid Tests

    [TestCase]
    public void TestGridCreatesCorrectCellCount()
    {
        var tiles = new[] { "grass", "water", "sand" };
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles);

        AssertThat(grid.CellCount).IsEqual(_testMesh.Vertices.Count);
    }

    [TestCase]
    public void TestAllCellsHaveAllTilesInitially()
    {
        var tiles = new[] { "grass", "water", "sand" };
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles);

        foreach (var vertex in _testMesh.Vertices)
        {
            var cell = grid.GetCell(vertex.Id);
            AssertThat(cell.GetPossibleTiles().Count).IsEqual(3);
        }
    }

    [TestCase]
    public void TestIsValidVertexReturnsTrueForExistingVertex()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        AssertBool(grid.IsValidVertex(0)).IsTrue();
        AssertBool(grid.IsValidVertex(_testMesh.Vertices.Count - 1)).IsTrue();
    }

    [TestCase]
    public void TestIsValidVertexReturnsFalseForInvalidId()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        AssertBool(grid.IsValidVertex(-1)).IsFalse();
        AssertBool(grid.IsValidVertex(1000)).IsFalse();
    }

    [TestCase]
    public void TestGetNeighborsReturnsAdjacentVertices()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        var neighbors = grid.GetNeighbors(4).ToList(); // Center vertex

        // Center vertex should have adjacent vertices
        AssertThat(neighbors.Count).IsGreater(0);
    }

    [TestCase]
    public void TestIsFullyCollapsedReturnsFalseInitially()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        AssertBool(grid.IsFullyCollapsed()).IsFalse();
    }

    [TestCase]
    public void TestIsFullyCollapsedReturnsTrueWhenAllCollapsed()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        // Collapse all cells
        foreach (var vertexId in grid.GetAllVertexIds())
        {
            grid.GetCell(vertexId).CollapseTo("grass");
        }

        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void TestHasContradictionReturnsFalseInitially()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        AssertBool(grid.HasContradiction()).IsFalse();
    }

    [TestCase]
    public void TestHasCollapsedNeighborReturnsFalseInitially()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        AssertBool(grid.HasCollapsedNeighbor(0)).IsFalse();
    }

    [TestCase]
    public void TestHasCollapsedNeighborReturnsTrueAfterNeighborCollapse()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        // Collapse vertex 0
        grid.GetCell(0).CollapseTo("grass");

        // Vertex 1 should now have a collapsed neighbor
        var vertex1Neighbors = grid.GetNeighbors(1);
        if (vertex1Neighbors.Contains(0))
        {
            AssertBool(grid.HasCollapsedNeighbor(1)).IsTrue();
        }
    }

    [TestCase]
    public void TestGetCollapsedTileAtReturnsNullWhenNotCollapsed()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        AssertThat(grid.GetCollapsedTileAt(0)).IsNull();
    }

    [TestCase]
    public void TestGetCollapsedTileAtReturnsTileWhenCollapsed()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        grid.GetCell(0).CollapseTo("water");

        AssertThat(grid.GetCollapsedTileAt(0)).IsEqual("water");
    }

    [TestCase]
    public void TestApplyToMeshUpdatesTerrain()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass" });

        // Collapse all cells
        foreach (var vertexId in grid.GetAllVertexIds())
        {
            grid.GetCell(vertexId).CollapseTo("grass");
        }

        var mapping = new Dictionary<string, int> { { "grass", 1 } };
        grid.ApplyToMesh(mapping);

        foreach (var vertex in _testMesh.Vertices)
        {
            AssertThat(vertex.TerrainType).IsEqual(1);
        }
    }

    [TestCase]
    public void TestCloneCreatesIndependentCopy()
    {
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, new[] { "grass", "water" });

        var clone = grid.Clone();
        grid.GetCell(0).CollapseTo("grass");

        // Clone should be unaffected
        AssertBool(clone.GetCell(0).IsCollapsed()).IsFalse();
    }

    #endregion

    #region MeshWfcSolver Tests

    [TestCase]
    public void TestPermissiveSolverCreation()
    {
        var tiles = new[] { "grass", "water", "sand" };
        var solver = IrregularMeshNs.MeshWfcSolver.CreatePermissive(tiles);

        AssertThat(solver).IsNotNull();
    }

    [TestCase]
    public void TestSolveWithPermissiveRulesSucceeds()
    {
        var tiles = new[] { "grass", "water", "sand" };
        var solver = IrregularMeshNs.MeshWfcSolver.CreatePermissive(tiles);
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var result = solver.Solve(grid, null, rng);

        AssertBool(result.Success).IsTrue();
        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void TestSolveWithAdjacencyRulesSucceeds()
    {
        var rules = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "sand" } },
            { "sand", new HashSet<string> { "grass", "sand", "water" } },
            { "water", new HashSet<string> { "sand", "water" } }
        };

        var solver = new IrregularMeshNs.MeshWfcSolver(rules);
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, rules.Keys);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var result = solver.Solve(grid, null, rng);

        AssertBool(result.Success).IsTrue();
    }

    [TestCase]
    public void TestSolveProducesValidAdjacency()
    {
        var rules = new Dictionary<string, HashSet<string>>
        {
            { "grass", new HashSet<string> { "grass", "sand" } },
            { "sand", new HashSet<string> { "grass", "sand", "water" } },
            { "water", new HashSet<string> { "sand", "water" } }
        };

        var solver = new IrregularMeshNs.MeshWfcSolver(rules);
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, rules.Keys);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        solver.Solve(grid, null, rng);

        // Verify all adjacencies are valid
        foreach (var vertexId in grid.GetAllVertexIds())
        {
            var tile = grid.GetCollapsedTileAt(vertexId);
            AssertThat(tile).IsNotNull();

            var allowedNeighbors = rules[tile!];
            foreach (var neighborId in grid.GetNeighbors(vertexId))
            {
                var neighborTile = grid.GetCollapsedTileAt(neighborId);
                AssertBool(allowedNeighbors.Contains(neighborTile!))
                    .OverrideFailureMessage($"Invalid adjacency: {tile} adjacent to {neighborTile}")
                    .IsTrue();
            }
        }
    }

    [TestCase]
    public void TestSolveReportsIterationCount()
    {
        var tiles = new[] { "grass" };
        var solver = IrregularMeshNs.MeshWfcSolver.CreatePermissive(tiles);
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var result = solver.Solve(grid, null, rng);

        AssertThat(result.Iterations).IsGreater(0);
    }

    [TestCase]
    public void TestSolveWithRetryReturnsGrid()
    {
        var tiles = new[] { "grass", "water" };
        var solver = IrregularMeshNs.MeshWfcSolver.CreatePermissive(tiles);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var (result, grid) = solver.SolveWithRetry(
            () => new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles),
            null,
            12345);

        AssertBool(result.Success).IsTrue();
        AssertThat(grid).IsNotNull();
        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void TestSolveWithTileWeights()
    {
        var tiles = new[] { "grass", "water" };
        var rules = tiles.ToDictionary(t => t, _ => new HashSet<string>(tiles));
        var weights = new Dictionary<string, float>
        {
            { "grass", 10.0f },
            { "water", 1.0f }
        };

        var solver = new IrregularMeshNs.MeshWfcSolver(rules, weights);
        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, tiles);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        solver.Solve(grid, null, rng);

        // Count tiles - grass should be more common due to higher weight
        var grassCount = grid.GetAllVertexIds().Count(v => grid.GetCollapsedTileAt(v) == "grass");
        var waterCount = grid.GetAllVertexIds().Count(v => grid.GetCollapsedTileAt(v) == "water");

        // With 10:1 weight ratio, grass should be significantly more common
        AssertThat(grassCount).IsGreater(waterCount);
    }

    [TestCase]
    public void TestMaxIterationsLimitsExecution()
    {
        // Create an impossible constraint set that would normally loop forever
        var rules = new Dictionary<string, HashSet<string>>
        {
            { "a", new HashSet<string> { "b" } }, // a can only be next to b
            { "b", new HashSet<string> { "a" } }  // b can only be next to a
        };

        var solver = new IrregularMeshNs.MeshWfcSolver(rules)
        {
            MaxIterations = 100 // Low limit
        };

        var grid = new IrregularMeshNs.MeshWfcGrid(_testMesh, rules.Keys);

        var rng = new RandomNumberGenerator();
        rng.Seed = 12345;

        var result = solver.Solve(grid, null, rng);

        // Should either succeed (lucky) or fail with max iterations
        if (!result.Success)
        {
            AssertThat(result.ErrorMessage).Contains("iterations");
        }
    }

    #endregion

    /// <summary>
    /// Create a simple 2x2 quad test mesh with 9 vertices.
    /// </summary>
    private static IrregularMeshNs.IrregularMesh CreateSimpleTestMesh()
    {
        var mesh = new IrregularMeshNs.IrregularMesh();

        // Create 3x3 grid of vertices
        var positions = new[]
        {
            new Vector2(0, 0), new Vector2(16, 0), new Vector2(32, 0),
            new Vector2(0, 16), new Vector2(16, 16), new Vector2(32, 16),
            new Vector2(0, 32), new Vector2(16, 32), new Vector2(32, 32)
        };

        foreach (var pos in positions)
        {
            mesh.AddVertex(pos);
        }

        // Add 4 quads
        mesh.AddQuad(new[] { 0, 1, 4, 3 });
        mesh.AddQuad(new[] { 1, 2, 5, 4 });
        mesh.AddQuad(new[] { 3, 4, 7, 6 });
        mesh.AddQuad(new[] { 4, 5, 8, 7 });

        mesh.UpdateAllCachedProperties();
        mesh.BuildAdjacency();

        return mesh;
    }
}
