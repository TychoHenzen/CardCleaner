using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Combat;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Simple turn-based combat system
/// </summary>
public class SimpleCombatSystem
{
    /// <summary>
    /// Combat participant that implements ICombatant for command pattern integration.
    /// </summary>
    internal sealed class Combatant : ICombatant
    {
        public required string Name { get; set; }
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

    private readonly Combatant _player;
    private readonly Combatant _enemy;
    private readonly List<CombatCommand> _playerCommands;
    private readonly CombatCommandInvoker _commandInvoker;
    private readonly RandomNumberGenerator _rng;
    private bool _playerTurn = true;

    public bool CombatComplete => !_player.IsAlive || !_enemy.IsAlive;
    public bool PlayerWon => !_enemy.IsAlive && _player.IsAlive;

    public event Action<string>? CombatLogUpdated;
    public event Action? CombatEnded;

    public SimpleCombatSystem(List<CardSignature> playerAbilities, CardSignature enemySeed, RandomNumberGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(enemySeed);

        _rng = rng;
        _commandInvoker = new CombatCommandInvoker();
        _commandInvoker.CommandExecuted += log => CombatLogUpdated?.Invoke(log);

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

        // Convert player abilities to combat commands
        _playerCommands = (playerAbilities ?? new List<CardSignature>())
            .Select(CombatCommand.FromCardSignature).ToList();

        ILog.Print($"=== COMBAT STARTED ===");
        ILog.Print($"Player: {_player.Health} HP");
        ILog.Print($"Enemy: {_enemy.Name} ({_enemy.Health} HP, {_enemy.AttackPower} ATK)");
        ILog.Print($"Player has {_playerCommands.Count} abilities");

        foreach (var command in _playerCommands) ILog.Print($"  - {command.Name}: {command.Description}");
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
        var context = new CombatContext { Source = _player, Target = _enemy };

        if (_playerCommands.Count == 0)
        {
            // Basic attack if no abilities
            var basicAttack = new CombatCommand("Basic Attack", _player.AttackPower, 0,
                $"Deals {_player.AttackPower} damage");
            _commandInvoker.ExecuteCommand(basicAttack, context);
            return;
        }

        // Choose random command from available abilities
        var command = _playerCommands[_rng.RandiRange(0, _playerCommands.Count - 1)];

        // Create a fresh command instance for execution (commands track their own state)
        var executionCommand = new CombatCommand(command.Name, command.Damage, command.Healing, command.Description);
        var logMessage = _commandInvoker.ExecuteCommand(executionCommand, context);

        if (logMessage != null)
            ILog.Print(logMessage);
    }

    private void ProcessEnemyTurn()
    {
        // Simple enemy AI: just attack
        var damage = _enemy.AttackPower + _rng.RandiRange(-2, 3);
        damage = Mathf.Max(1, damage);

        var context = new CombatContext { Source = _enemy, Target = _player };
        var attackCommand = new CombatCommand("Attack", damage, 0, $"Deals {damage} damage");
        var logMessage = _commandInvoker.ExecuteCommand(attackCommand, context);

        if (logMessage != null)
            ILog.Print(logMessage);
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

    /// <summary>
    /// Undo the last combat action. Returns true if an action was undone.
    /// </summary>
    public bool UndoLastAction()
    {
        return _commandInvoker.UndoLastCommand();
    }

    /// <summary>
    /// Number of actions that can be undone.
    /// </summary>
    public int UndoableActionCount => _commandInvoker.HistoryCount;
}