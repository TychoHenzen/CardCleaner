using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.SolverScenarios;

/// <summary>
///     The solver must keep the passability graph in step with collapsed cells.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcSolverPassabilityGraphTest
{
    private static readonly string[] Tiles = { "grass", "wall" };

    private WfcAdjacencyRules _rules = null!;

    [BeforeTest]
    public void Setup() => _rules = new WfcAdjacencyRules(new[] { ("grass", "wall") });

    private static bool IsPassable(string tileId) => tileId == "grass";

    private sealed record SolvedMap(WfcGrid Grid, PassabilityGraph Graph);

    private SolvedMap Solve(ulong seed)
    {
        var graph = new PassabilityGraph();
        var solver = new WfcSolver(
            new WfcPropagator(_rules), new WfcTileSelector(), null, graph, IsPassable);
        var grid = new WfcGrid(5, 5, Tiles);

        var result = solver.Solve(grid, null, new RandomNumberGenerator { Seed = seed });

        AssertBool(result.Success).IsTrue();
        return new SolvedMap(grid, graph);
    }

    [TestCase]
    public void Solve_AddsExactlyTheCollapsedPassableCellsToGraph()
    {
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var (grid, graph) = Solve(seed);
            var passableCount = 0;
            foreach (var position in grid.GetAllPositions())
            {
                var passable = IsPassable(grid.GetCollapsedTileAt(position)!);
                AssertBool(graph.ContainsNode(position)).IsEqual(passable);
                if (passable)
                    passableCount++;
            }

            AssertInt(passableCount).IsGreater(0);
            AssertInt(graph.NodeCount).IsEqual(passableCount);
        }
    }

    [TestCase]
    public void Solve_ConnectsAdjacentPassableCellsInGraph()
    {
        for (ulong seed = 1; seed <= 4; seed++)
        {
            var (grid, graph) = Solve(seed);

            foreach (var position in grid.GetAllPositions())
            {
                if (!graph.ContainsNode(position))
                    continue;

                foreach (var neighbor in grid.GetNeighbors(position))
                {
                    if (graph.ContainsNode(neighbor))
                        AssertBool(graph.AreConnected(position, neighbor)).IsTrue();
                }
            }
        }
    }

    [TestCase]
    public void Solve_ResetsGraphSoRetriesDoNotInheritEarlierAttempts()
    {
        var graph = new PassabilityGraph();
        var solver = new WfcSolver(
            new WfcPropagator(_rules), new WfcTileSelector(), null, graph, IsPassable);

        for (ulong seed = 1; seed <= 3; seed++)
        {
            var grid = new WfcGrid(5, 5, Tiles);
            AssertBool(solver.Solve(grid, null, new RandomNumberGenerator { Seed = seed }).Success).IsTrue();

            var passableCount = 0;
            foreach (var position in grid.GetAllPositions())
            {
                var passable = IsPassable(grid.GetCollapsedTileAt(position)!);
                AssertBool(graph.ContainsNode(position)).IsEqual(passable);
                if (passable)
                    passableCount++;
            }

            AssertThat(graph.NodeCount).IsEqual(passableCount);
        }
    }
}
