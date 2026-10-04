using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.SimpleCombatScenarios;

/// <summary>
///     CombatCommand creation, naming and invoker scenarios split out of SimpleCombatSystemTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CombatCommandTest : SimpleCombatTestBase
{
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
}
