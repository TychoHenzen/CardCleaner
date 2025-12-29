using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;
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
    public void TestCombatCommandFromPositiveSignature()
    {
        var signature = new CardSignature(new[] { 0.8f, 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var command = CombatCommand.FromCardSignature(signature);

        AssertThat(command).IsNotNull();
        AssertThat(command.Damage).IsGreater(0);
        AssertThat(command.Name).IsNotNull();
        AssertThat(command.Description).IsNotNull();
    }

    [TestCase]
    public void TestCombatCommandFromNegativeSignature()
    {
        var signature = new CardSignature(new[] { -0.8f, -0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var command = CombatCommand.FromCardSignature(signature);

        AssertThat(command).IsNotNull();
        AssertThat(command.Healing).IsGreater(0);
    }

    [TestCase]
    public void TestCombatCommandMinimumDamage()
    {
        var signature = new CardSignature(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var command = CombatCommand.FromCardSignature(signature);

        AssertThat(command.Damage).IsGreaterEqual(1);
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
    public void TestCombatCommandNaming()
    {
        var solidumSignature = new CardSignature(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var command = CombatCommand.FromCardSignature(solidumSignature);

        AssertThat(command.Name).IsEqual("Earth Strike");
    }

    [TestCase]
    public void TestCombatCommandFireBlast()
    {
        var febrisSignature = new CardSignature(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var command = CombatCommand.FromCardSignature(febrisSignature);

        AssertThat(command.Name).IsEqual("Fire Blast");
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
    public void TestCombatCommandCanExecuteReturnsFalseWhenAlreadyExecuted()
    {
        var command = new CombatCommand("Test", 10, 0, "Test damage");
        var source = new SimpleCombatSystem.Combatant { Name = "Source", Health = 50, MaxHealth = 50 };
        var target = new SimpleCombatSystem.Combatant { Name = "Target", Health = 50, MaxHealth = 50 };
        var context = new CombatContext { Source = source, Target = target };

        AssertBool(command.CanExecute(context)).IsTrue();

        command.Execute(context);

        AssertBool(command.CanExecute(context)).IsFalse();
    }

    [TestCase]
    public void TestCombatCommandInvokerExecutesAndTracksCommands()
    {
        var invoker = new CombatCommandInvoker();
        var command = new CombatCommand("Test", 10, 0, "Test");
        var source = new SimpleCombatSystem.Combatant { Name = "Source", Health = 50, MaxHealth = 50 };
        var target = new SimpleCombatSystem.Combatant { Name = "Target", Health = 50, MaxHealth = 50 };
        var context = new CombatContext { Source = source, Target = target };

        var log = invoker.ExecuteCommand(command, context);

        AssertThat(log).IsNotNull();
        AssertThat(invoker.HistoryCount).IsEqual(1);
        AssertThat(target.Health).IsEqual(40);
    }

    [TestCase]
    public void TestCombatCommandInvokerUndoRestoresState()
    {
        var invoker = new CombatCommandInvoker();
        var command = new CombatCommand("Test", 10, 5, "Test");
        var source = new SimpleCombatSystem.Combatant { Name = "Source", Health = 40, MaxHealth = 50 };
        var target = new SimpleCombatSystem.Combatant { Name = "Target", Health = 50, MaxHealth = 50 };
        var context = new CombatContext { Source = source, Target = target };

        invoker.ExecuteCommand(command, context);

        AssertThat(target.Health).IsEqual(40);
        AssertThat(source.Health).IsEqual(45);

        invoker.UndoLastCommand();

        AssertThat(target.Health).IsEqual(50);
        AssertThat(source.Health).IsEqual(40);
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
