using System;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Plays a timer-driven mock combat on the combat UI and reports who won once one side runs out of health.
/// </summary>
internal sealed class CombatVisualization
{
    private const int PlayerMaxHealth = 50;
    private const int EnemyMaxHealth = 30;

    private readonly Node _host;
    private readonly WorldMapCombatUI? _combatUI;
    private readonly Action<bool> _onFinished;
    private int _playerHealth = PlayerMaxHealth;
    private int _enemyHealth = EnemyMaxHealth;

    internal CombatVisualization(Node host, WorldMapCombatUI? combatUI, Action<bool> onFinished)
    {
        _host = host;
        _combatUI = combatUI;
        _onFinished = onFinished;
    }

    internal void Start()
    {
        var timer = new Timer();
        _host.AddChild(timer);
        timer.WaitTime = 1.0f;
        timer.Timeout += () => PlayRound(timer);
        timer.Start();
    }

    private void PlayRound(Timer timer)
    {
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        if (rng.Randf() > 0.5f)
        {
            _enemyHealth -= rng.RandiRange(8, 15);
            _combatUI?.UpdateAction("Player attacks!");
        }
        else
        {
            _playerHealth -= rng.RandiRange(5, 10);
            _combatUI?.UpdateAction("Enemy attacks!");
        }

        _combatUI?.UpdateHealth(_playerHealth, PlayerMaxHealth, _enemyHealth, EnemyMaxHealth);

        if (_enemyHealth <= 0)
            Finish(timer, "Victory!", true);
        else if (_playerHealth <= 0)
            Finish(timer, "Defeat!", false);
    }

    private void Finish(Timer timer, string message, bool playerWon)
    {
        _combatUI?.UpdateAction(message);
        timer.QueueFree();
        _onFinished(playerWon);
    }
}
