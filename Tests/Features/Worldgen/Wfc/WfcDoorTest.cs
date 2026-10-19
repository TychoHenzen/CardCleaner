using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using WfcDoor = CardCleaner.Scripts.Features.Worldgen.Wfc.Wfc;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Pins the Wfc front door: the solver and generator it creates are fresh instances over fresh rules, and the
/// generator it creates keeps its retry count and accepts null variants.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcDoorTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void CreateTerrainSolverReportsTheRuleTileIdsOfFreshRules()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        var solver = WfcDoor.CreateTerrainSolver(pairs, null);
        var expected = new WfcAdjacencyRules(pairs);

        AssertString(string.Join("|", solver.RuleTileIds)).IsEqual(string.Join("|", expected.AllTileIds));
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CreateMapGeneratorRoundTripsMaxRetriesAndAcceptsNullVariants()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        var generator = WfcDoor.CreateMapGenerator(pairs, null);
        AssertInt(generator.MaxRetries).IsEqual(3);

        generator.MaxRetries = 5;
        generator.SetSelectedVariants(null);
        AssertInt(generator.MaxRetries).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EachCallCreatesANewInstance()
    {
        var pairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();

        AssertBool(ReferenceEquals(WfcDoor.CreateMapGenerator(pairs, null), WfcDoor.CreateMapGenerator(pairs, null)))
            .IsFalse();
        AssertBool(ReferenceEquals(WfcDoor.CreateTerrainSolver(pairs, null), WfcDoor.CreateTerrainSolver(pairs, null)))
            .IsFalse();
    }
}
