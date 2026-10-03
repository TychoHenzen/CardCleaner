using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Handles combat encounters on the irregular mesh map.
/// Manages combat flow, enemy signatures, and combat resolution.
/// </summary>
public class IrregularMapCombatHandler
{
    private readonly IrregularMeshMapData _mapData;
    private readonly RandomNumberGenerator _rng;
    private List<CardSignature> _abilityCards;
    private SimpleCombatSystem? _combatSystem;
    private int _currentEnemyCellId = -1;

    /// <summary>
    /// Raised when combat ends.
    /// </summary>
    public event Action<int, bool>? CombatEnded;

    public IrregularMapCombatHandler(
        IrregularMeshMapData mapData,
        RandomNumberGenerator rng,
        List<CardSignature>? abilityCards = null)
    {
        _mapData = mapData;
        _rng = rng;
        _abilityCards = abilityCards ?? new List<CardSignature>();
    }

    /// <summary>
    /// Update ability cards (called when starting a new map).
    /// </summary>
    public void SetAbilityCards(List<CardSignature> abilityCards)
    {
        _abilityCards = abilityCards;
    }

    /// <summary>
    /// Start combat with the enemy at the given cell.
    /// </summary>
    /// <param name="enemyCellId">The cell ID where the enemy is located.</param>
    /// <param name="signalSource">Node to use for async signals (awaiting timers).</param>
    public void StartCombat(int enemyCellId, Node signalSource)
    {
        if (_abilityCards.Count == 0)
        {
            GD.PrintErr("[IrregularMapCombatHandler] No ability cards for combat!");
            return;
        }

        _currentEnemyCellId = enemyCellId;

        // Generate enemy signature based on cell position for variety
        var cellCenter = _mapData.GetCellCenter(enemyCellId);
        var positionSeed = (int)(cellCenter.X * 1000 + cellCenter.Y * 31);
        var enemyRng = new RandomNumberGenerator { Seed = (ulong)positionSeed };
        var enemySignature = CardSignature.Random(enemyRng);

        GD.Print(
            $"[IrregularMapCombatHandler] Starting combat at cell {enemyCellId} " +
            $"with {_abilityCards.Count} ability cards");

        // Create combat system
        _combatSystem = new SimpleCombatSystem(_abilityCards, enemySignature, _rng);
        _combatSystem.CombatEnded += () => OnCombatEnded(signalSource);

        // Process combat turns automatically
        ProcessCombatTurnsAsync(signalSource);
    }

    private async void ProcessCombatTurnsAsync(Node signalSource)
    {
        if (_combatSystem == null) return;

        while (_combatSystem != null && !_combatSystem.CombatComplete)
        {
            _combatSystem.ProcessTurn();

            // Small delay between turns for visibility
            await signalSource.ToSignal(signalSource.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
        }
    }

    private void OnCombatEnded(Node signalSource)
    {
        var playerWon = _combatSystem?.PlayerWon ?? false;
        var cellId = _currentEnemyCellId;

        GD.Print($"[IrregularMapCombatHandler] Combat ended - Player {(playerWon ? "won" : "lost")}!");

        _combatSystem = null;
        _currentEnemyCellId = -1;

        CombatEnded?.Invoke(cellId, playerWon);
    }

    /// <summary>
    /// Whether combat is currently in progress.
    /// </summary>
    public bool IsInCombat => _combatSystem != null && !_combatSystem.CombatComplete;

    /// <summary>
    /// The cell ID of the current enemy being fought.
    /// </summary>
    public int CurrentEnemyCellId => _currentEnemyCellId;
}
