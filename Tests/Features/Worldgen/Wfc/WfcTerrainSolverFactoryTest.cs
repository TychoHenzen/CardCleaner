using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Pins IWfcTerrainSolver.IFactory: each Create call returns a fresh solver, and the solver reports the rule tile
/// ids of fresh rules.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcTerrainSolverFactoryTest
{
    [TestCase]
    public static void CreateReturnsANewSolverEachCall()
    {
        var pairs = GameTransitionResolver.Shared.GetAllTransitionPairs().ToList();
        IWfcTerrainSolver.IFactory factory = new WfcMapGenerator.Factory();

        AssertBool(ReferenceEquals(factory.Create(pairs, null), factory.Create(pairs, null))).IsFalse();
    }

    [TestCase]
    public static void CreateReportsTheRuleTileIdsOfFreshRules()
    {
        var pairs = GameTransitionResolver.Shared.GetAllTransitionPairs().ToList();

        var solver = WfcMapGenerator.Factory.Build(pairs, null);
        var expected = new WfcAdjacencyRules(pairs);

        AssertString(string.Join("|", solver.RuleTileIds)).IsEqual(string.Join("|", expected.AllTileIds));
    }
}
