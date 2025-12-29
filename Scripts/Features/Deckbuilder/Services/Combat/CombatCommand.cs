using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;

/// <summary>
/// Combat command that deals damage to a target and optionally heals the source.
/// Implements the Command pattern with full undo capability.
/// </summary>
public class CombatCommand : ICombatCommand
{
    private int _damageDealt;
    private int _healingApplied;
    private bool _executed;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <summary>
    /// Base damage this command will deal (before any modifiers).
    /// </summary>
    public int Damage { get; }

    /// <summary>
    /// Base healing this command will apply to the source.
    /// </summary>
    public int Healing { get; }

    public CombatCommand(string name, int damage, int healing, string description)
    {
        Name = name;
        Damage = damage;
        Healing = healing;
        Description = description;
    }

    /// <inheritdoc />
    public bool CanExecute(CombatContext context)
    {
        return context.Source.IsAlive && context.Target.IsAlive && !_executed;
    }

    /// <inheritdoc />
    public string Execute(CombatContext context)
    {
        if (!CanExecute(context))
            return $"Cannot execute {Name}";

        _executed = true;
        var logMessage = $"{context.Source.Name} uses {Name}!";

        if (Damage > 0)
        {
            var healthBefore = context.Target.Health;
            context.Target.TakeDamage(Damage);
            _damageDealt = healthBefore - context.Target.Health;
            logMessage += $" Deals {_damageDealt} damage!";
        }

        if (Healing > 0)
        {
            var healthBefore = context.Source.Health;
            context.Source.Heal(Healing);
            _healingApplied = context.Source.Health - healthBefore;
            logMessage += $" Heals {_healingApplied} HP!";
        }

        return logMessage;
    }

    /// <inheritdoc />
    public void Undo(CombatContext context)
    {
        if (!_executed)
            return;

        // Reverse healing first, then damage
        if (_healingApplied > 0)
        {
            context.Source.TakeDamage(_healingApplied);
        }

        if (_damageDealt > 0)
        {
            context.Target.Heal(_damageDealt);
        }

        _executed = false;
        _damageDealt = 0;
        _healingApplied = 0;
    }

    /// <summary>
    /// Create a combat command from a card signature.
    /// </summary>
    public static CombatCommand FromCardSignature(CardSignature signature)
    {
        var totalPower = 0f;
        var totalHealing = 0f;

        for (var i = 0; i < 8; i++)
        {
            var value = signature[i];
            if (value > 0.3f)
                totalPower += value * 10f;
            else if (value < -0.3f)
                totalHealing += Mathf.Abs(value) * 8f;
        }

        var strongestElementIndex = 0;
        var strongestValue = Mathf.Abs(signature[0]);
        for (var i = 1; i < 8; i++)
        {
            if (Mathf.Abs(signature[i]) > strongestValue)
            {
                strongestValue = Mathf.Abs(signature[i]);
                strongestElementIndex = i;
            }
        }

        var actionName = strongestElementIndex switch
        {
            0 => "Earth Strike",
            1 => "Fire Blast",
            2 => "Order Shield",
            3 => "Light Ray",
            4 => "Space Warp",
            5 => "Heavy Slam",
            6 => "Aid Spell",
            7 => "Distance Shot",
            _ => "Card Action"
        };

        var damage = Mathf.RoundToInt(Mathf.Max(1, totalPower));
        var healing = Mathf.RoundToInt(totalHealing);

        return new CombatCommand(
            actionName,
            damage,
            healing,
            $"Deals {damage} damage" + (healing > 0 ? $" and heals {healing}" : "")
        );
    }
}
