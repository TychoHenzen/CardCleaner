using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public partial class GameSessionService : Node, IGameSessionService
{
    private SessionState _currentState = SessionState.WaitingForCards;
    private CardSignature _mapSeed;
    private List<CardSignature> _abilityCards = new();
    private RandomNumberGenerator _rng = new();

    public SessionState CurrentState 
    { 
        get => _currentState;
        private set
        {
            if (_currentState != value)
            {
                _currentState = value;
                StateChanged?.Invoke(value);
                GD.Print($"Session state changed to: {value}");
            }
        }
    }

    public event Action<SessionState> StateChanged;
    public event Action<List<CardSignature>> LootGenerated;

    public override void _Ready()
    {
        _rng.Randomize();
    }

    public void StartSession(CardSignature mapSeed, List<CardSignature> abilityCards)
    {
        if (CurrentState != SessionState.WaitingForCards)
        {
            GD.PrintErr("Cannot start session - session already in progress");
            return;
        }

        if (mapSeed == null || abilityCards == null || abilityCards.Count == 0)
        {
            GD.PrintErr("Cannot start session with null or empty inputs");
            return;
        }

        _mapSeed = mapSeed;
        _abilityCards = new List<CardSignature>(abilityCards);
        CurrentState = SessionState.GeneratingMap;
        
        GD.Print($"Started session with map seed and {_abilityCards.Count} ability cards");
        
        // Auto-advance to next phase
        CallDeferred(nameof(AdvanceSession));
    }

    public void AdvanceSession()
    {
        switch (CurrentState)
        {
            case SessionState.GeneratingMap:
                GenerateMap();
                break;
            case SessionState.Exploring:
                ExploreMap();
                break;
            case SessionState.InCombat:
                ResolveCombat();
                break;
            case SessionState.GeneratingLoot:
                GenerateLoot();
                break;
            case SessionState.SessionComplete:
                GD.Print("Session already complete");
                break;
            default:
                GD.PrintErr($"Cannot advance from state: {CurrentState}");
                break;
        }
    }

    public void ResetSession()
    {
        _mapSeed = null;
        _abilityCards.Clear();
        CurrentState = SessionState.WaitingForCards;
        GD.Print("Session reset");
    }

    private void GenerateMap()
    {
        GD.Print($"Generating map from seed signature: {_mapSeed}");
        
        // TODO: Implement actual map generation from card signatures
        // For now, just simulate map generation
        
        CurrentState = SessionState.Exploring;
        
        // Auto-advance after brief delay
        GetTree().CreateTimer(0.5f).Timeout += () => CallDeferred(nameof(AdvanceSession));
    }

    private void ExploreMap()
    {
        GD.Print("Autonomous agent exploring map...");
        
        // TODO: Implement actual exploration logic
        // For now, immediately find an enemy
        
        CurrentState = SessionState.InCombat;
        
        // Auto-advance after brief delay  
        GetTree().CreateTimer(0.5f).Timeout += () => CallDeferred(nameof(AdvanceSession));
    }

    private void ResolveCombat()
    {
        GD.Print($"Resolving combat with {_abilityCards.Count} ability cards...");
        
        // TODO: Implement actual combat resolution using ability cards
        // For now, always win combat
        
        CurrentState = SessionState.GeneratingLoot;
        
        // Auto-advance after brief delay
        GetTree().CreateTimer(0.5f).Timeout += () => CallDeferred(nameof(AdvanceSession));
    }

    private void GenerateLoot()
    {
        GD.Print("Generating loot from defeated enemy...");
        
        // Generate ~10 card signatures as specified
        var lootSignatures = new List<CardSignature>();
        
        // TODO: Generate based on enemy signature sphere around map seed
        // For now, create signatures with some relation to the map seed
        for (int i = 0; i < 10; i++)
        {
            // Create signatures that are variations of the map seed
            var lootSignature = new CardSignature();
            for (int j = 0; j < 8; j++)
            {
                // Add some random variation around the map seed signature
                var baseValue = _mapSeed[j];
                var variation = _rng.RandfRange(-0.3f, 0.3f);
                lootSignature[j] = Mathf.Clamp(baseValue + variation, -1f, 1f);
            }
            lootSignatures.Add(lootSignature);
        }
        
        CurrentState = SessionState.SessionComplete;
        LootGenerated?.Invoke(lootSignatures);
        
        GD.Print($"Session complete! Generated {lootSignatures.Count} loot signatures");
    }
}
