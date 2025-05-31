using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public enum SessionState
{
    WaitingForCards,
    GeneratingMap,
    Exploring, 
    InCombat,
    GeneratingLoot,
    SessionComplete
}

public interface IGameSessionService
{
    SessionState CurrentState { get; }
    event Action<SessionState> StateChanged;
    event Action<List<CardSignature>> LootGenerated;
    
    void StartSession(CardSignature mapSeed, List<CardSignature> abilityCards);
    void AdvanceSession();
    void ResetSession();
}
