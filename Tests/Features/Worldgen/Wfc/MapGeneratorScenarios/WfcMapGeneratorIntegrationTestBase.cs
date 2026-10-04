using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     Shared fixture for the WfcMapGeneratorIntegrationTest scenario suites.
/// </summary>
public abstract class WfcMapGeneratorIntegrationTestBase
{
    protected CompiledTransitionResolver _resolver = null!;

    protected BiomeRegistry _biomeRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        _resolver = new CompiledTransitionResolver();
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();
    }
}
