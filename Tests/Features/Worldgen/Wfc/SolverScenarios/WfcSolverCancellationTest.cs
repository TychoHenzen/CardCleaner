using System;
using System.Linq;
using System.Threading;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.SolverScenarios;

/// <summary>
///     A cancelled token stops a running solve within one collapsed cell.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcSolverCancellationTest
{
    private const ulong Seed = 7;
    private const int GridSize = 8;
    private static readonly string[] Tiles = { "grass", "wall" };

    // Every tile may border every other, so propagation never narrows a cell and the only collapsed cells are the
    // ones the solver collapsed itself.
    private static WfcSolver CreateSolver(WfcTileSelector selector) =>
        new(new WfcPropagator(new WfcAdjacencyRules(new[] { ("grass", "wall") })), selector);

    private static int CountCollapsed(WfcGrid grid) =>
        grid.GetAllPositions().Count(position => grid.GetCollapsedTileAt(position) != null);

    /// <summary>Cancels the token the first time it is asked to weigh a tile after a cell has collapsed.</summary>
    private sealed class CancelAfterFirstCollapse(WfcGrid grid, CancellationTokenSource source) : IWfcConstraint
    {
        public int? CollapsedAtCancel { get; private set; }

        public float GetProbabilityModifier(WfcConstraintContext context)
        {
            if (CollapsedAtCancel == null && CountCollapsed(grid) >= 1)
            {
                CollapsedAtCancel = CountCollapsed(grid);
                source.Cancel();
            }

            return 1f;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Solve_CancelledWhileRunning_StopsWithinOneCollapsedCell()
    {
        using var source = new CancellationTokenSource();
        var grid = new WfcGrid(GridSize, GridSize, Tiles);
        var canceller = new CancelAfterFirstCollapse(grid, source);
        var selector = new WfcTileSelector();
        selector.AddConstraint(canceller);

        AssertThrown(() => CreateSolver(selector).Solve(grid, null, RandomSources.Create(Seed), source.Token))
            .IsInstanceOf<OperationCanceledException>();

        AssertThat(canceller.CollapsedAtCancel).IsNotNull();
        AssertInt(CountCollapsed(grid)).IsLessEqual(canceller.CollapsedAtCancel!.Value + 1);
        AssertInt(CountCollapsed(grid)).IsLess(GridSize * GridSize);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Solve_TokenCancelledBeforeTheSolve_CollapsesNoCell()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var grid = new WfcGrid(GridSize, GridSize, Tiles);

        AssertThrown(() => CreateSolver(new WfcTileSelector())
                .Solve(grid, null, RandomSources.Create(Seed), source.Token))
            .IsInstanceOf<OperationCanceledException>();

        AssertInt(CountCollapsed(grid)).IsEqual(0);
    }
}
