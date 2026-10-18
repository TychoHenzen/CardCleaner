using System;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// One turn-based fight between the player's ability cards and an enemy.
/// </summary>
public interface ICombatSystem
{
    event Action? CombatEnded;

    bool PlayerWon { get; }

    /// <summary>Executes one combat turn. Returns true if combat should continue.</summary>
    bool ProcessTurn();
}
