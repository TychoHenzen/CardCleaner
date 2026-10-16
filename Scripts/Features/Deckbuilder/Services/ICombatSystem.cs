using System;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>One turn-based fight between the player and an enemy.</summary>
public interface ICombatSystem
{
    bool PlayerWon { get; }

    event Action? CombatEnded;

    /// <summary>Plays one turn. Returns true while the fight continues.</summary>
    bool ProcessTurn();
}
