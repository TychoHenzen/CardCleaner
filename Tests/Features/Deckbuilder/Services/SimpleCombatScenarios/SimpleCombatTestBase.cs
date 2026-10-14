using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleCombatScenarios;

/// <summary>
///     Shared fixture for the SimpleCombatSystem scenario suites.
/// </summary>
public abstract class SimpleCombatTestBase
{
    protected IRandom _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = RandomSources.Create(12345);
    }
}
