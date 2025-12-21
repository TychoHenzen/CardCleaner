using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class SimpleCombatSystemTest
{
    private RandomNumberGenerator _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = new RandomNumberGenerator();
        _rng.Seed = 12345;
    }

    [TestCase]
    public void TestCombatSystemInitialization()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();

        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        AssertBool(combat.CombatComplete).IsFalse();
        var (playerHealth, playerMaxHealth, enemyHealth, enemyMaxHealth) = combat.GetCombatStatus();
        AssertThat(playerHealth).IsEqual(playerMaxHealth);
        AssertThat(enemyHealth).IsEqual(enemyMaxHealth);
    }

    [TestCase]
    public void TestCombatActionFromPositiveSignature()
    {
        var signature = new CardSignature(new[] { 0.8f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var action = SimpleCombatSystem.CombatAction.FromCardSignature(signature);

        AssertThat(action).IsNotNull();
        AssertThat(action.Damage).IsGreater(0);
        AssertThat(action.Name).IsNotNull();
        AssertThat(action.Description).IsNotNull();
    }

    [TestCase]
    public void TestCombatActionFromNegativeSignature()
    {
        var signature = new CardSignature(new[] { -0.8f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var action = SimpleCombatSystem.CombatAction.FromCardSignature(signature);

        AssertThat(action).IsNotNull();
        AssertThat(action.Healing).IsGreater(0);
    }

    [TestCase]
    public void TestCombatActionMinimumDamage()
    {
        var signature = new CardSignature(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var action = SimpleCombatSystem.CombatAction.FromCardSignature(signature);

        AssertThat(action.Damage).IsGreaterEqual(1);
    }

    [TestCase]
    public void TestProcessTurnReturnsTrueWhileCombatContinues()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        var result = combat.ProcessTurn();

        AssertBool(result || combat.CombatComplete).IsTrue();
    }

    [TestCase]
    public void TestPlayerWonAfterEnemyDefeated()
    {
        var strongAbility = new CardSignature(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f });
        var abilities = new List<CardSignature> { strongAbility };
        var weakEnemy = new CardSignature(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var combat = new SimpleCombatSystem(abilities, weakEnemy, _rng);

        while (!combat.CombatComplete)
        {
            combat.ProcessTurn();
        }

        var (playerHealth, _, enemyHealth, _) = combat.GetCombatStatus();
        AssertBool(combat.PlayerWon == (enemyHealth == 0 && playerHealth > 0)).IsTrue();
    }

    [TestCase]
    public void TestCombatEndsEventually()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        var turnCount = 0;
        while (!combat.CombatComplete && turnCount < 1000)
        {
            combat.ProcessTurn();
            turnCount++;
        }

        AssertBool(combat.CombatComplete).IsTrue();
    }

    [TestCase]
    public void TestCombatEndedEventFires()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);
        var eventFired = false;

        combat.CombatEnded += () => eventFired = true;

        while (!combat.CombatComplete)
        {
            combat.ProcessTurn();
        }

        AssertBool(eventFired).IsTrue();
    }

    [TestCase]
    public void TestCombatLogUpdatedEventFires()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);
        var logMessages = new List<string>();

        combat.CombatLogUpdated += msg => logMessages.Add(msg);

        combat.ProcessTurn();
        combat.ProcessTurn();

        AssertThat(logMessages.Count).IsGreaterEqual(1);
    }

    [TestCase]
    public void TestGetCombatStatusReturnsValidData()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        var (playerHealth, playerMaxHealth, enemyHealth, enemyMaxHealth) = combat.GetCombatStatus();

        AssertThat(playerHealth).IsBetween(0, playerMaxHealth);
        AssertThat(enemyHealth).IsBetween(0, enemyMaxHealth);
        AssertThat(playerMaxHealth).IsEqual(50);
        AssertThat(enemyMaxHealth).IsGreater(0);
    }

    [TestCase]
    public void TestCombatantHealthDecreasesOverTime()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        var (initialPlayerHealth, _, initialEnemyHealth, _) = combat.GetCombatStatus();

        combat.ProcessTurn();
        combat.ProcessTurn();

        var (afterPlayerHealth, _, afterEnemyHealth, _) = combat.GetCombatStatus();

        var someDamageDealt = afterPlayerHealth < initialPlayerHealth || afterEnemyHealth < initialEnemyHealth;
        AssertBool(someDamageDealt).IsTrue();
    }

    [TestCase]
    public void TestEnemyPowerScalesWithSignatureIntensity()
    {
        var abilities = new List<CardSignature> { new CardSignature() };

        var weakSeed = new CardSignature(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var weakCombat = new SimpleCombatSystem(abilities, weakSeed, _rng);
        var (_, _, weakEnemyHealth, _) = weakCombat.GetCombatStatus();

        var strongSeed = new CardSignature(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f });
        var strongCombat = new SimpleCombatSystem(abilities, strongSeed, _rng);
        var (_, _, strongEnemyHealth, _) = strongCombat.GetCombatStatus();

        AssertThat(strongEnemyHealth).IsGreater(weakEnemyHealth);
    }

    [TestCase]
    public void TestCombatWithMultipleAbilities()
    {
        var abilities = new List<CardSignature>
        {
            new CardSignature(new[] { 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new CardSignature(new[] { 0.0f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new CardSignature(new[] { 0.0f, 0.0f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        AssertBool(combat.CombatComplete).IsFalse();

        while (!combat.CombatComplete)
        {
            combat.ProcessTurn();
        }

        AssertBool(combat.CombatComplete).IsTrue();
    }

    [TestCase]
    public void TestCombatActionNaming()
    {
        var solidumSignature = new CardSignature(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var action = SimpleCombatSystem.CombatAction.FromCardSignature(solidumSignature);

        AssertThat(action.Name).IsEqual("Earth Strike");
    }

    [TestCase]
    public void TestCombatActionFireBlast()
    {
        var febrisSignature = new CardSignature(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var action = SimpleCombatSystem.CombatAction.FromCardSignature(febrisSignature);

        AssertThat(action.Name).IsEqual("Fire Blast");
    }

    [TestCase]
    public void TestCombatWithEmptyAbilitiesUsesBasicAttack()
    {
        var abilities = new List<CardSignature>();
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        combat.ProcessTurn();

        var (_, _, enemyHealth, enemyMaxHealth) = combat.GetCombatStatus();
        AssertThat(enemyHealth).IsLess(enemyMaxHealth);
    }

    [TestCase]
    public void TestProcessTurnReturnsFalseWhenCombatComplete()
    {
        var abilities = new List<CardSignature> { new CardSignature() };
        var enemySeed = new CardSignature();
        var combat = new SimpleCombatSystem(abilities, enemySeed, _rng);

        while (!combat.CombatComplete)
        {
            combat.ProcessTurn();
        }

        var result = combat.ProcessTurn();

        AssertBool(result).IsFalse();
    }

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
}
