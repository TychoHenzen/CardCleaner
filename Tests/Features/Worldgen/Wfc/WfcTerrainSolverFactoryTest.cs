using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

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
    [TestCategory("Unit")]
    public static void CreateReturnsANewSolverEachCall()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        IWfcTerrainSolver.IFactory factory = new WfcMapGenerator.Factory();

        AssertBool(ReferenceEquals(factory.Create(pairs, null), factory.Create(pairs, null))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CreateReportsTheRuleTileIdsOfFreshRules()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        var solver = WfcMapGenerator.Factory.Build(pairs, null);
        var expected = new WfcAdjacencyRules(pairs);

        AssertString(string.Join("|", solver.RuleTileIds)).IsEqual(string.Join("|", expected.AllTileIds));
    }
}
