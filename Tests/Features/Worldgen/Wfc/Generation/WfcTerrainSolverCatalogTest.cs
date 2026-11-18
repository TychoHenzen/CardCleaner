using System;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Generation;

/// <summary>
/// Pins that a terrain solver built without a tile catalog refuses the catalog-backed solve instead of running it
/// with no catalog constraints.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcTerrainSolverCatalogTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void SolveGraphWithCatalogWithoutCatalogThrows()
    {
        var transitionPairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        var solver = WfcMapGenerator.Factory.Build(transitionPairs, null);

        AssertThrown(() => solver.SolveGraphWithCatalog(Array.Empty<int[]>(), Array.Empty<string>(), 1UL, null))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("SolveGraphWithCatalog needs a tile catalog");
    }
}
