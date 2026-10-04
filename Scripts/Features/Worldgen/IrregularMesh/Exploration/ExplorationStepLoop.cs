using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Exploration;

/// <summary>
/// Repeatedly steps the exploration AI and animates the movement controller to each new cell.
/// </summary>
internal sealed class ExplorationStepLoop
{
    private readonly ExplorationAI _explorationAI;
    private readonly IrregularMeshMovementController _movementController;
    private readonly ExplorationPathDisplay _pathDisplay;
    private readonly Func<SignalAwaiter?> _waitForStepDelay;
    private readonly Func<bool> _autoExplore;
    private bool _processingStep;

    internal ExplorationStepLoop(
        ExplorationAI explorationAI,
        IrregularMeshMovementController movementController,
        ExplorationPathDisplay pathDisplay,
        Func<SignalAwaiter?> waitForStepDelay,
        Func<bool> autoExplore)
    {
        _explorationAI = explorationAI;
        _movementController = movementController;
        _pathDisplay = pathDisplay;
        _waitForStepDelay = waitForStepDelay;
        _autoExplore = autoExplore;
    }

    /// <summary>
    /// Whether exploration is currently in progress.
    /// </summary>
    internal bool IsExploring { get; set; }

    /// <summary>
    /// Raised when the displayed path changes.
    /// </summary>
    internal event Action<IReadOnlyList<int>>? PathUpdated;

    /// <summary>
    /// Raised when the AI has no more cells to explore. Not raised when exploration stops for an enemy encounter,
    /// because combat resumes it afterwards.
    /// </summary>
    internal event Action? Finished;

    internal void Start()
    {
        if (IsExploring)
            return;

        IsExploring = true;
        ProcessNextExplorationStep();
    }

    private async void ProcessNextExplorationStep()
    {
        // Guard against re-entrance
        if (_processingStep || !IsExploring)
            return;

        _processingStep = true;
        try
        {
            var targetCell = AdvanceExploration();
            if (!targetCell.HasValue)
                return;

            // Animate to where the AI moved
            await _movementController.MoveToCellAsync(targetCell.Value);

            // Wait before next step
            var delay = _waitForStepDelay();
            if (delay != null)
            {
                await delay;
            }

            // Continue exploration if still active
            if (IsExploring && _autoExplore())
            {
                _processingStep = false; // Allow next step
                ProcessNextExplorationStep();
                return;
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[IrregularMeshExplorationController] Error in exploration step: {ex.Message}");
        }
        finally
        {
            _processingStep = false;
        }
    }

    /// <summary>
    /// Takes the next step in the exploration AI and publishes the remaining path.
    /// Returns the cell the AI moved to, or null when exploration finished.
    /// </summary>
    private int? AdvanceExploration()
    {
        var stepped = _explorationAI.StepExploration();

        if (!stepped || _explorationAI.HasFinishedExploration)
        {
            IsExploring = false;
            if (!_explorationAI.HasFoundEnemy)
                Finished?.Invoke();

            return null;
        }

        // After StepExploration, the AI has already moved internally to the next cell.
        // The visual movement is animated to where the AI now is.
        var targetCell = _explorationAI.CurrentCellId;

        // Update path display (shows remaining path after current position)
        var remainingPath = _explorationAI.CurrentPath;
        if (remainingPath != null)
        {
            PathUpdated?.Invoke(_pathDisplay.ShowFromCell(targetCell, remainingPath));
        }

        return targetCell;
    }
}
