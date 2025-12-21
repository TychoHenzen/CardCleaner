using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Simple turn-based combat system
/// </summary>
public class SimpleCombatSystem
{
    public class Combatant
    {
        public string Name { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int AttackPower { get; set; }
        public bool IsPlayer { get; set; }

        public bool IsAlive => Health > 0;

        public void TakeDamage(int damage)
        {
            Health = Mathf.Max(0, Health - damage);
            ILog.Print($"{Name} takes {damage} damage! Health: {Health}/{MaxHealth}");
        }

        public void Heal(int amount)
        {
            Health = Mathf.Min(MaxHealth, Health + amount);
            ILog.Print($"{Name} heals for {amount}! Health: {Health}/{MaxHealth}");
        }
    }

    public class CombatAction
    {
        public string Name { get; set; }
        public int Damage { get; set; }
        public int Healing { get; set; }
        public string Description { get; set; }

        public static CombatAction FromCardSignature(CardSignature signature)
        {
            // Convert card signature into combat action
            // Use signature values to determine action properties

            var totalPower = 0f;
            var totalHealing = 0f;
            var actionName = "Card Action";

            // Calculate power from signature elements
            for (var i = 0; i < 8; i++)
            {
                var value = signature[i];
                if (value > 0.3f) // Positive elements contribute to damage
                    totalPower += value * 10f; // Scale to reasonable damage range
                else if (value < -0.3f) // Negative elements contribute to healing
                    totalHealing += Mathf.Abs(value) * 8f;
            }

            // Determine action type based on strongest element
            var strongestElementIndex = 0;
            var strongestValue = Mathf.Abs(signature[0]);
            for (var i = 1; i < 8; i++)
                if (Mathf.Abs(signature[i]) > strongestValue)
                {
                    strongestValue = Mathf.Abs(signature[i]);
                    strongestElementIndex = i;
                }

            actionName = strongestElementIndex switch
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

            return new CombatAction
            {
                Name = actionName,
                Damage = Mathf.RoundToInt(Mathf.Max(1, totalPower)),
                Healing = Mathf.RoundToInt(totalHealing),
                Description = $"Deals {Mathf.RoundToInt(totalPower)} damage" +
                              (totalHealing > 0 ? $" and heals {Mathf.RoundToInt(totalHealing)}" : "")
            };
        }
    }

    private readonly Combatant _player;
    private readonly Combatant _enemy;
    private readonly List<CombatAction> _playerActions;
    private readonly RandomNumberGenerator _rng;
    private bool _playerTurn = true;

    public bool CombatComplete => !_player.IsAlive || !_enemy.IsAlive;
    public bool PlayerWon => !_enemy.IsAlive && _player.IsAlive;

    public event Action<string> CombatLogUpdated;
    public event Action CombatEnded;

    public SimpleCombatSystem(List<CardSignature> playerAbilities, CardSignature enemySeed, RandomNumberGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(enemySeed);

        _rng = rng;

        // Create player
        _player = new Combatant
        {
            Name = "Player",
            Health = 50,
            MaxHealth = 50,
            AttackPower = 10,
            IsPlayer = true
        };

        // Create enemy based on seed signature
        var enemyPower = CalculateEnemyPower(enemySeed);
        _enemy = new Combatant
        {
            Name = "Shadow Beast",
            Health = Mathf.RoundToInt(30 + enemyPower * 5),
            MaxHealth = Mathf.RoundToInt(30 + enemyPower * 5),
            AttackPower = Mathf.RoundToInt(8 + enemyPower * 2),
            IsPlayer = false
        };

        // Convert player abilities to combat actions
        _playerActions = (playerAbilities ?? new List<CardSignature>())
            .Select(CombatAction.FromCardSignature).ToList();

        ILog.Print($"=== COMBAT STARTED ===");
        ILog.Print($"Player: {_player.Health} HP");
        ILog.Print($"Enemy: {_enemy.Name} ({_enemy.Health} HP, {_enemy.AttackPower} ATK)");
        ILog.Print($"Player has {_playerActions.Count} abilities");

        foreach (var action in _playerActions) ILog.Print($"  - {action.Name}: {action.Description}");
    }

    /// <summary>
    /// Execute one combat turn. Returns true if combat should continue.
    /// </summary>
    public bool ProcessTurn()
    {
        if (CombatComplete) return false;

        if (_playerTurn)
            ProcessPlayerTurn();
        else
            ProcessEnemyTurn();

        _playerTurn = !_playerTurn;

        if (CombatComplete)
        {
            var result = PlayerWon ? "VICTORY!" : "DEFEAT!";
            ILog.Print($"=== COMBAT ENDED: {result} ===");
            CombatEnded?.Invoke();
            return false;
        }

        return true;
    }

    private void ProcessPlayerTurn()
    {
        if (_playerActions.Count == 0)
        {
            // Basic attack if no abilities
            _enemy.TakeDamage(_player.AttackPower);
            CombatLogUpdated?.Invoke($"Player attacks for {_player.AttackPower} damage!");
            return;
        }

        // Choose random action from available abilities
        var action = _playerActions[_rng.RandiRange(0, _playerActions.Count - 1)];

        var logMessage = $"Player uses {action.Name}!";

        if (action.Damage > 0)
        {
            _enemy.TakeDamage(action.Damage);
            logMessage += $" Deals {action.Damage} damage!";
        }

        if (action.Healing > 0)
        {
            _player.Heal(action.Healing);
            logMessage += $" Heals {action.Healing} HP!";
        }

        ILog.Print(logMessage);
        CombatLogUpdated?.Invoke(logMessage);
    }

    private void ProcessEnemyTurn()
    {
        // Simple enemy AI: just attack
        var damage = _enemy.AttackPower + _rng.RandiRange(-2, 3); // Add some variance
        damage = Mathf.Max(1, damage);

        _player.TakeDamage(damage);

        var logMessage = $"{_enemy.Name} attacks for {damage} damage!";
        ILog.Print(logMessage);
        CombatLogUpdated?.Invoke(logMessage);
    }

    private static float CalculateEnemyPower(CardSignature signature)
    {
        // Calculate overall "intensity" of the signature to determine enemy strength
        var totalIntensity = 0f;
        for (var i = 0; i < 8; i++) totalIntensity += Mathf.Abs(signature[i]);
        return Mathf.Clamp(totalIntensity / 8f, 0.1f, 1.0f);
    }

    public (int playerHealth, int playerMaxHealth, int enemyHealth, int enemyMaxHealth) GetCombatStatus()
    {
        return (_player.Health, _player.MaxHealth, _enemy.Health, _enemy.MaxHealth);
    }
}