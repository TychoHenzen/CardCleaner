using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Controls smooth visual movement between quad centroids on an irregular mesh.
/// Provides position tracking using quad IDs and handles movement animation.
/// </summary>
public partial class IrregularMeshMovementController : Node2D
{
    private IMapData? _mapData;
    private int _currentCellId;
    private bool _isMoving;
    private Tween? _movementTween;

    /// <summary>
    /// Duration of movement animation between quads in seconds.
    /// </summary>
    [Export]
    public float MoveDuration { get; set; } = 0.15f;

    /// <summary>
    /// Easing type for movement animation.
    /// </summary>
    [Export]
    public Tween.TransitionType TransitionType { get; set; } = Tween.TransitionType.Sine;

    /// <summary>
    /// Easing mode for movement animation.
    /// </summary>
    [Export]
    public Tween.EaseType EaseType { get; set; } = Tween.EaseType.InOut;

    /// <summary>
    /// The current cell (quad) ID.
    /// </summary>
    public int CurrentCellId => _currentCellId;

    /// <summary>
    /// The current world position (center of current quad).
    /// </summary>
    public Vector2 CurrentWorldPosition => _mapData?.GetCellCenter(_currentCellId) ?? Position;

    /// <summary>
    /// Whether movement animation is in progress.
    /// </summary>
    public bool IsMoving => _isMoving;

    /// <summary>
    /// Raised when movement to a new cell begins.
    /// Parameters: (fromCellId, toCellId, fromWorldPos, toWorldPos)
    /// </summary>
    public event Action<int, int, Vector2, Vector2>? MovementStarted;

    /// <summary>
    /// Raised when movement to a new cell completes.
    /// Parameters: (newCellId, worldPos)
    /// </summary>
    public event Action<int, Vector2>? MovementCompleted;

    /// <summary>
    /// Raised when a cell is entered (after movement completes).
    /// Parameters: (cellId)
    /// </summary>
    public event Action<int>? CellEntered;

    /// <summary>
    /// Raised when position updates during movement (for smooth tracking).
    /// Parameters: (worldPos)
    /// </summary>
    public event Action<Vector2>? PositionUpdated;

    /// <summary>
    /// Initialize the controller with map data and starting cell.
    /// </summary>
    /// <param name="mapData">The map data for position lookups.</param>
    /// <param name="startCellId">The starting cell ID.</param>
    public void Initialize(IMapData mapData, int startCellId)
    {
        _mapData = mapData;
        _currentCellId = startCellId;
        Position = _mapData.GetCellCenter(startCellId);
    }

    /// <summary>
    /// Instantly teleport to a cell without animation.
    /// </summary>
    /// <param name="cellId">The cell ID to teleport to.</param>
    public void TeleportToCell(int cellId)
    {
        if (_mapData == null || !_mapData.IsValidCell(cellId))
            return;

        CancelMovement();

        var oldCellId = _currentCellId;
        _currentCellId = cellId;
        Position = _mapData.GetCellCenter(cellId);

        if (oldCellId != cellId)
            CellEntered?.Invoke(cellId);

        PositionUpdated?.Invoke(Position);
    }

    /// <summary>
    /// Move to an adjacent cell with smooth animation.
    /// </summary>
    /// <param name="targetCellId">The target cell ID (must be adjacent and passable).</param>
    /// <returns>True if movement started, false if invalid target.</returns>
    public bool MoveToCell(int targetCellId)
    {
        if (_mapData == null || _isMoving)
            return false;

        if (!_mapData.IsValidCell(targetCellId))
            return false;

        if (!_mapData.IsPassable(targetCellId))
            return false;

        if (!_mapData.GetAdjacentCells(_currentCellId).Contains(targetCellId))
            return false;

        StartMovement(targetCellId);
        return true;
    }

    /// <summary>
    /// Move to an adjacent cell with smooth animation (async).
    /// </summary>
    /// <param name="targetCellId">The target cell ID.</param>
    /// <returns>Task that completes when movement finishes.</returns>
    public async Task MoveToCellAsync(int targetCellId)
    {
        if (!MoveToCell(targetCellId))
            return;

        // Wait for movement to complete
        while (_isMoving)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    /// <summary>
    /// Move along a path of cell IDs with smooth animation.
    /// </summary>
    /// <param name="path">The path of cell IDs to follow.</param>
    /// <param name="onCellReached">Optional callback for each cell reached.</param>
    /// <returns>Task that completes when entire path is traversed.</returns>
    public async Task MoveAlongPathAsync(IEnumerable<int> path, Action<int>? onCellReached = null)
    {
        foreach (var cellId in path)
        {
            if (cellId == _currentCellId)
                continue;

            await MoveToCellAsync(cellId);
            onCellReached?.Invoke(cellId);
        }
    }

    /// <summary>
    /// Cancel any ongoing movement and snap to current position.
    /// </summary>
    public void CancelMovement()
    {
        if (_movementTween != null && _movementTween.IsValid())
        {
            _movementTween.Kill();
            _movementTween = null;
        }
        _isMoving = false;
    }

    /// <summary>
    /// Get the world position for a cell ID.
    /// </summary>
    public Vector2 GetCellWorldPosition(int cellId)
    {
        return _mapData?.GetCellCenter(cellId) ?? Vector2.Zero;
    }

    /// <summary>
    /// Find the cell at a world position.
    /// </summary>
    public int? GetCellAtPosition(Vector2 worldPos)
    {
        return _mapData?.GetCellAtPosition(worldPos);
    }

    public override void _Process(double delta)
    {
        if (_isMoving)
        {
            PositionUpdated?.Invoke(Position);
        }
    }

    private void StartMovement(int targetCellId)
    {
        if (_mapData == null)
            return;

        var fromPos = Position;
        var toPos = _mapData.GetCellCenter(targetCellId);
        var fromCellId = _currentCellId;

        _isMoving = true;
        MovementStarted?.Invoke(fromCellId, targetCellId, fromPos, toPos);

        // Create movement tween
        _movementTween = CreateTween();
        _movementTween.SetTrans(TransitionType);
        _movementTween.SetEase(EaseType);

        _movementTween.TweenProperty(this, "position", toPos, MoveDuration);
        _movementTween.TweenCallback(Callable.From(() => OnMovementComplete(targetCellId, toPos)));
    }

    private void OnMovementComplete(int newCellId, Vector2 newPos)
    {
        _currentCellId = newCellId;
        _isMoving = false;
        _movementTween = null;

        Position = newPos; // Ensure exact position
        MovementCompleted?.Invoke(newCellId, newPos);
        CellEntered?.Invoke(newCellId);
        PositionUpdated?.Invoke(newPos);
    }
}
