using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TerrainComposition;

/// <summary>
///     Shared fixture for the TerrainCompositionContextTest scenario suites.
/// </summary>
public abstract class TerrainCompositionContextTestBase
{
    protected TileRegistry _registry = null!;

    protected CompiledTransitionResolver _resolver = null!;

    [BeforeTest]
    public void Setup()
    {
        _registry = new TileRegistry();
        _resolver = new CompiledTransitionResolver();
    }
}
