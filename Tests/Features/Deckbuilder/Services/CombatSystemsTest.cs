using System.Collections.Generic;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class CombatSystemsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public void CreateReturnsACombatThatRunsToItsEndAndRaisesCombatEnded()
    {
        var abilities = new List<CardSignature> { new CardSignature() };

        ICombatSystem combat = CombatSystems.Create(abilities, new CardSignature(), RandomSources.Create(12345));

        var ended = 0;
        combat.CombatEnded += () => ended++;
        var turns = 0;
        while (combat.ProcessTurn() && turns < 1000) turns++;
        AssertBool(combat is SimpleCombatSystem).IsTrue();
        AssertInt(turns).IsLess(1000);
        AssertInt(ended).IsEqual(1);
    }
}
