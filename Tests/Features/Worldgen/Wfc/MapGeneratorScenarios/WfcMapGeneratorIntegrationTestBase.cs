using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     Shared fixture for the WfcMapGeneratorIntegrationTest scenario suites.
/// </summary>
public abstract class WfcMapGeneratorIntegrationTestBase
{
    protected IReadOnlyList<(string tileA, string tileB)> _transitionPairs = null!;

    protected BiomeRegistry _biomeRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        _transitionPairs = GameTransitionResolver.Shared.GetAllTransitionPairs().ToList();
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
    }
}
