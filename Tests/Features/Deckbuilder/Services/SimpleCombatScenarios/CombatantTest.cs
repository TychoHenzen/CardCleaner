using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleCombatScenarios;

/// <summary>
///     Combatant damage, healing and undo scenarios split out of SimpleCombatSystemTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CombatantTest : SimpleCombatTestBase
{
    [TestCase]
    public void TestCombatantTakeDamageMethod()
    {
        var combatant = new SimpleCombatSystem.Combatant
        {
            Name = "Test",
            Health = 50,
            MaxHealth = 50
        };

        combatant.TakeDamage(10);

        AssertThat(combatant.Health).IsEqual(40);
        AssertBool(combatant.IsAlive).IsTrue();
    }

    [TestCase]
    public void TestCombatantTakeDamageCannotGoBelowZero()
    {
        var combatant = new SimpleCombatSystem.Combatant
        {
            Name = "Test",
            Health = 10,
            MaxHealth = 50
        };

        combatant.TakeDamage(100);

        AssertThat(combatant.Health).IsEqual(0);
        AssertBool(combatant.IsAlive).IsFalse();
    }

    [TestCase]
    public void TestCombatantHealMethod()
    {
        var combatant = new SimpleCombatSystem.Combatant
        {
            Name = "Test",
            Health = 30,
            MaxHealth = 50
        };

        combatant.Heal(15);

        AssertThat(combatant.Health).IsEqual(45);
    }

    [TestCase]
    public void TestCombatantHealCannotExceedMaxHealth()
    {
        var combatant = new SimpleCombatSystem.Combatant
        {
            Name = "Test",
            Health = 45,
            MaxHealth = 50
        };

        combatant.Heal(100);

        AssertThat(combatant.Health).IsEqual(50);
    }

    [TestCase]
    public void TestUndoLastActionReversesDamage()
    {
        var abilities = new List<CardSignature>
        {
            new CardSignature(new[] { 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        var (_, _, initialEnemyHealth, _) = combat.GetCombatStatus();

        // Player turn deals damage
        combat.ProcessTurn();
        var (_, _, afterDamageHealth, _) = combat.GetCombatStatus();

        // Undo should restore enemy health
        combat.UndoLastAction();
        var (_, _, afterUndoHealth, _) = combat.GetCombatStatus();

        AssertThat(afterDamageHealth).IsLess(initialEnemyHealth);
        AssertThat(afterUndoHealth).IsEqual(initialEnemyHealth);
    }

    [TestCase]
    public void TestUndoableActionCountTracksHistory()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        AssertThat(combat.UndoableActionCount).IsEqual(0);

        combat.ProcessTurn();
        AssertThat(combat.UndoableActionCount).IsEqual(1);

        combat.ProcessTurn();
        AssertThat(combat.UndoableActionCount).IsEqual(2);

        combat.UndoLastAction();
        AssertThat(combat.UndoableActionCount).IsEqual(1);
    }

    [TestCase]
    public void TestCombatantImplementsICombatant()
    {
        var combatant = new SimpleCombatSystem.Combatant
        {
            Name = "Test",
            Health = 50,
            MaxHealth = 50
        };

        ICombatant iCombatant = combatant;

        AssertThat(iCombatant.Name).IsEqual("Test");
        AssertThat(iCombatant.Health).IsEqual(50);
        AssertThat(iCombatant.MaxHealth).IsEqual(50);
        AssertBool(iCombatant.IsAlive).IsTrue();
    }
}
