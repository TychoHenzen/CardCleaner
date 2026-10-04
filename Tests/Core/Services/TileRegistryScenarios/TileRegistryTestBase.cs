using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Tests.Core.Services.TileRegistryScenarios;

/// <summary>
///     Shared fixture for the TileRegistry scenario suites.
/// </summary>
public abstract class TileRegistryTestBase
{
    protected TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup() => _registry = new TileRegistry();
}
