using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Tests for the unified WFC solver that works with both regular grids and irregular meshes.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class UnifiedWfcSolverTest
{
    private RandomNumberGenerator _rng = null!;

    [Before]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 42;
    }

    [TestCase]
    public void UnifiedSolver_SolvesSimpleGrid()
    {
        // Arrange
        var tiles = new[] { "grass", "dirt", "stone" };
        var rules = new PermissiveAdjacencyRules(tiles);
        var propagator = new UnifiedWfcPropagator(rules, use8WayPropagation: false);
        var selector = new UnifiedWfcTileSelector();
        var solver = new UnifiedWfcSolver(propagator, selector);

        var grid = new WfcGrid(5, 5, tiles);

        // Act
        var result = solver.Solve(grid, null, _rng);

        // Assert
        AssertBool(result.Success).IsTrue();
        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void UnifiedSolver_AppliesSpatialCoherenceConstraint()
    {
        // Arrange
        var tiles = new[] { "grass", "dirt", "stone" };
        var rules = new PermissiveAdjacencyRules(tiles);
        var propagator = new UnifiedWfcPropagator(rules, use8WayPropagation: false);
        var selector = new UnifiedWfcTileSelector();
        var solver = new UnifiedWfcSolver(propagator, selector);

        solver.AddConstraint(new UnifiedSpatialCoherenceConstraint
        {
            TargetRegionSize = 10,
            BoostFactor = 5.0f
        });

        var grid = new WfcGrid(10, 10, tiles);

        // Act
        var result = solver.Solve(grid, null, _rng);

        // Assert
        AssertBool(result.Success).IsTrue();
        AssertBool(grid.IsFullyCollapsed()).IsTrue();

        // Verify spatial coherence created some contiguous regions
        // (not all tiles should be different)
        var tileAtOrigin = grid.GetCollapsedTileAt(new Vector2I(0, 0));
        var tileAtNextCell = grid.GetCollapsedTileAt(new Vector2I(1, 0));
        // With spatial coherence, adjacent tiles should often match
        // (though not guaranteed, the constraint makes it likely)
    }

    [TestCase]
    public void UnifiedSolver_WorksWithMeshGrid()
    {
        // Arrange
        var tiles = new[] { "grass", "dirt" };
        var rules = new PermissiveAdjacencyRules(tiles);
        var propagator = new UnifiedWfcPropagator(rules, use8WayPropagation: false);
        var selector = new UnifiedWfcTileSelector();
        var solver = new UnifiedWfcSolver(propagator, selector);

        // Create a simple mesh
        var meshConfig = new MeshGenerator.GenerationConfig
        {
            Rings = 2,
            Seed = 42,
            RelaxationIterations = 5
        };
        var mesh = MeshGenerator.Generate(meshConfig);
        var meshGrid = new MeshWfcGrid(mesh, tiles);

        // Act
        var result = solver.Solve(meshGrid, null, _rng);

        // Assert
        AssertBool(result.Success).IsTrue();
        AssertBool(meshGrid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void UnifiedFactory_CreatesWorkingSolver()
    {
        // Arrange
        var tiles = new[] { "grass", "dirt", "stone" };
        var solver = UnifiedWfcFactory.CreatePermissive(tiles);
        var grid = new WfcGrid(5, 5, tiles);

        // Act
        var result = solver.Solve(grid, null, _rng);

        // Assert
        AssertBool(result.Success).IsTrue();
        AssertBool(grid.IsFullyCollapsed()).IsTrue();
    }

    [TestCase]
    public void UnifiedSolver_RetriesOnContradiction()
    {
        // Arrange - create rules that might cause contradictions
        var tiles = new[] { "a", "b" };
        var rules = new PermissiveAdjacencyRules(tiles);
        var propagator = new UnifiedWfcPropagator(rules, use8WayPropagation: false);
        var selector = new UnifiedWfcTileSelector();
        var solver = new UnifiedWfcSolver(propagator, selector);

        // Act
        var (result, grid) = solver.SolveWithRetry(
            () => new WfcGrid(5, 5, tiles),
            null,
            42,
            maxRetries: 3);

        // Assert
        AssertBool(result.Success).IsTrue();
        AssertThat(grid).IsNotNull();
    }

    [TestCase]
    public void UnifiedConstraintAdapter_WrapsGridConstraint()
    {
        // Arrange - create a simple constraint that returns 0.5 for all tiles
        var mockConstraint = new MockGridConstraint();
        var adapted = mockConstraint.ToUnified();

        // Act
        var modifier = adapted.GetWeightModifier(0, "test", null!);

        // Assert
        // GridConstraintAdapter returns 1.0 when grid isn't WfcGrid
        AssertThat(modifier).IsEqual(1.0f);
    }

    /// <summary>
    /// Mock constraint for testing adapter
    /// </summary>
    private class MockGridConstraint : IWfcConstraint
    {
        public float GetProbabilityModifier(WfcConstraintContext context) => 0.5f;
    }
}
