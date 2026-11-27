using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;

namespace CardCleaner.Tests.Features.Deckbuilder.Tiles.Loading.TileRegistryScenarios;

/// <summary>
///     Shared fixture for the TileRegistry scenario suites.
/// </summary>
public abstract class TileRegistryTestBase
{
    protected TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup() => _registry = TileCatalogLoad.Load(new GodotAssetPaths(), new CompiledAtlasSource());
}
