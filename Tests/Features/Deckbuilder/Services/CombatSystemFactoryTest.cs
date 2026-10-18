using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

/// <summary>
///     The combat factory builds a combat system that plays to its end and raises CombatEnded once.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CombatSystemFactoryTest
{
    private const int TurnBound = 1000;

    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
        TestServices.RegisterGameServiceClasses(_container);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CreatedCombatEndsAndRaisesCombatEndedOnce()
    {
        var factory = _container.Resolve<ICombatSystemFactory>();
        var abilities = new List<CardSignature> { new CardSignature() };

        var combat = factory.Create(abilities, new CardSignature(), RandomSources.Create());
        var ended = 0;
        combat.CombatEnded += () => ended++;

        var turns = 0;
        while (combat.ProcessTurn() && turns < TurnBound)
            turns++;

        AssertBool(turns < TurnBound).IsTrue();
        AssertThat(ended).IsEqual(1);
    }
}
