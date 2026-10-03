using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleCombatScenarios;

/// <summary>
///     Shared fixture for the SimpleCombatSystem scenario suites.
/// </summary>
public abstract class SimpleCombatTestBase
{
    protected RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
    }
}
