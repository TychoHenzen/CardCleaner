using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// What the irregular screen shows of one session: the map view, the player sprite, every move as a smooth step from
/// cell to cell, the fog and defeated enemies. Every move the session reports is reported shown exactly once.
/// </summary>
internal sealed class IrregularMapPresenter
{
    private readonly SessionViewLink _link;
    private readonly Action<int, Vector2> _stepShown;

    private WorldMapSession? _view;

    // The move that animates now: reported shown when its animation ends, or at once when a later move arrives first.
    // Its visibility arrives while it animates and is shown when the player arrives.
    private (int Cell, Vector2 WorldPos)? _animatingStep;
    private (HashSet<int> Seen, HashSet<int> Visible)? _pendingVisibility;

    // stepShown gets the cell and world position of each move once the player shows there.
    internal IrregularMapPresenter(SessionViewLink link, Action<int, Vector2> stepShown)
    {
        _link = link;
        _stepShown = stepShown;
    }

    internal Sprite2D? PlayerSprite { get; set; }

    internal IrregularMesh? Mesh { get; private set; }

    internal IrregularMeshMapData? MapData { get; private set; }

    internal IrregularMeshFogOfWar? FogOfWar => _view?.FogOfWar;

    internal bool HasView => _view != null;

    internal void ShowMap(IMeshGeneratedMap map, WorldMapSettings settings)
    {
        CloseView();
        Mesh = map.RawMesh;
        var view = new WorldMapSession(settings, map);
        _view = view;
        MapData = view.MapData;

        var startCell = view.Build();
        if (view.Movement != null)
        {
            view.Movement.PositionUpdated += OnPositionUpdated;
            view.Movement.MovementCompleted += OnMovementCompleted;
        }

        UpdatePlayerPosition(startCell);
        GD.Print($"[IrregularWorldMapScreen] Showing map: {map.RawMesh.GetStatistics()}");
    }

    internal void CloseView()
    {
        if (_view?.Movement is { } movement)
        {
            movement.PositionUpdated -= OnPositionUpdated;
            movement.MovementCompleted -= OnMovementCompleted;
        }

        _view?.Close();
        _view = null;
        Mesh = null;
        MapData = null;
        _animatingStep = null;
        _pendingVisibility = null;
    }

    internal void RevealAll() => _view?.RevealAll();

    /// <summary>The screen no longer owns the session: visibility still waiting for an animation is dropped.</summary>
    internal void ReleaseSession() => _pendingVisibility = null;

    /// <summary>
    /// Animates the player to the cell the session moved it to, and reports the move shown when the player arrives. A
    /// move to a cell that is not next to the player is shown at once, and a move that arrives while the last one
    /// still animates first ends that animation.
    /// </summary>
    internal void OnPlayerMoved(Vector2 worldPos)
    {
        if (_animatingStep is { } previous)
        {
            _view?.Movement?.TeleportToCell(previous.Cell);
            FinishStep(previous.Cell, previous.WorldPos);
        }

        var cellId = _view?.MapData.GetCellAtPosition(worldPos);
        if (_view?.Movement is not { } movement || cellId is not { } cell)
        {
            _link.ReportStepShown();
            return;
        }

        if (movement.MoveToCell(cell))
        {
            _animatingStep = (cell, worldPos);
            return;
        }

        movement.TeleportToCell(cell);
        FinishStep(cell, worldPos);
    }

    internal void OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells)
    {
        if (_view?.FogOfWar == null) return;

        _pendingVisibility = (new HashSet<int>(seenCells), new HashSet<int>(visibleCells));
        if (_animatingStep == null)
            ApplyPendingVisibility();
    }

    /// <summary>Removes the defeated enemy and puts the player on its cell, where the session placed it.</summary>
    internal void OnEnemyDefeated(int cellId)
    {
        _view?.RemoveEnemyAt(cellId);
        if (_animatingStep == null && _view?.Movement is { } movement)
        {
            movement.TeleportToCell(cellId);
            UpdatePlayerPosition(cellId);
        }
    }

    private void OnMovementCompleted(int cellId, Vector2 worldPos)
    {
        if (_animatingStep is { } step && step.Cell == cellId)
            FinishStep(step.Cell, step.WorldPos);
    }

    private void FinishStep(int cellId, Vector2 worldPos)
    {
        _animatingStep = null;
        UpdatePlayerPosition(cellId);
        ApplyPendingVisibility();
        _stepShown(cellId, worldPos);

        _link.ReportStepShown();
    }

    private void ApplyPendingVisibility()
    {
        if (_pendingVisibility is not { } update || !_link.OwnsSession) return;

        _pendingVisibility = null;
        _view?.FogOfWar?.UpdateVisibility(update.Seen, update.Visible);
    }

    private void UpdatePlayerPosition(int cellId)
    {
        if (MapData == null || PlayerSprite == null) return;
        PlayerSprite.Position = MapData.GetCellCenter(cellId);
    }

    private void OnPositionUpdated(Vector2 worldPos)
    {
        if (PlayerSprite != null)
            PlayerSprite.Position = worldPos;
    }
}
