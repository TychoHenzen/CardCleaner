using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Tests.TestUtilities;

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
        _registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
        _resolver = GameTransitionResolver.Shared;
    }
}
