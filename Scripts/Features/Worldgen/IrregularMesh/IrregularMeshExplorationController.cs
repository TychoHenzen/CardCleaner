using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Integrates ExplorationAI with IrregularMeshMovementController for
/// smooth visual exploration on irregular mesh terrain.
/// </summary>
public partial class IrregularMeshExplorationController : Node2D
{
    private IrregularMeshMapData? _mapData;
    private ExplorationAI? _explorationAI;
    private IrregularMeshMovementController? _movementController;
    private Pathfinder? _pathfinder;
    private IVisibilityChecker? _visibilityChecker;
    private IrregularMeshFogOfWar? _fogOfWar;

    private bool _isExploring;
    private bool _processingStep;
    private List<int>? _currentPath;
    private int _pathIndex;

    /// <summary>
    /// Delay between exploration steps in seconds.
    /// </summary>
    [Export]
    public float StepDelay { get; set; } = 0.1f;

    /// <summary>
    /// Whether to automatically continue exploration after each step.
    /// </summary>
    [Export]
    public bool AutoExplore { get; set; } = true;

    /// <summary>
    /// The current cell ID being occupied.
    /// </summary>
    public int CurrentCellId => _movementController?.CurrentCellId ?? -1;

    /// <summary>
    /// The current world position.
    /// </summary>
    public Vector2 CurrentWorldPosition => _movementController?.CurrentWorldPosition ?? Vector2.Zero;

    /// <summary>
    /// Whether exploration is currently in progress.
    /// </summary>
    public bool IsExploring => _isExploring;

    /// <summary>
    /// Whether exploration has finished (no more cells to explore).
    /// </summary>
    public bool HasFinishedExploration => _explorationAI?.HasFinishedExploration ?? false;

    /// <summary>
    /// The current exploration mode.
    /// </summary>
    public ExplorationMode CurrentMode => _explorationAI?.CurrentMode ?? ExplorationMode.FrontierExploration;

    /// <summary>
    /// Set of visited cell IDs.
    /// </summary>
    public IReadOnlySet<int> VisitedCells => _explorationAI?.SeenCells ?? new HashSet<int>();

    /// <summary>
    /// The fog of war system (null if not initialized).
    /// </summary>
    public IrregularMeshFogOfWar? FogOfWar => _fogOfWar;

    /// <summary>
    /// Current path as world positions.
    /// </summary>
    public IReadOnlyList<Vector2> CurrentPathPositions
    {
        get
        {
            if (_currentPath == null || _mapData == null)
                return Array.Empty<Vector2>();

            return _currentPath
                .Select(id => _mapData.GetCellCenter(id))
                .ToList();
        }
    }

    /// <summary>
    /// Raised when the player moves to a new cell.
    /// Parameters: (cellId, worldPos)
    /// </summary>
    public event Action<int, Vector2>? PlayerMoved;

    /// <summary>
    /// Raised when the player's world position updates during movement animation.
    /// Parameters: (worldPos)
    /// </summary>
    public event Action<Vector2>? PositionUpdated;

    /// <summary>
    /// Raised when the current path changes.
    /// Parameters: (path as cell IDs)
    /// </summary>
    public event Action<IReadOnlyList<int>>? PathUpdated;

    /// <summary>
    /// Raised when visible cells change.
    /// Parameters: (seenCells, currentlyVisibleCells)
    /// </summary>
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityUpdated;

    /// <summary>
    /// Raised when an enemy is spotted.
    /// Parameters: (enemyCellId)
    /// </summary>
    public event Action<int>? EnemySpotted;

    /// <summary>
    /// Raised when an enemy is encountered (same cell).
    /// Parameters: (enemyCellId)
    /// </summary>
    public event Action<int>? EnemyEncountered;

    /// <summary>
    /// Raised when exploration finishes.
    /// </summary>
    public event Action? ExplorationFinished;

    public override void _Ready()
    {
        // Create movement controller as child
        _movementController = new IrregularMeshMovementController();
        AddChild(_movementController);

        _movementController.MovementCompleted += OnMovementCompleted;
        _movementController.CellEntered += OnCellEntered;
        _movementController.PositionUpdated += OnPositionUpdated;
    }

    /// <summary>
    /// Initialize the exploration controller.
    /// </summary>
    /// <param name="mapData">The irregular mesh map data.</param>
    /// <param name="startCellId">The starting cell ID.</param>
    /// <param name="visibilityChecker">Optional visibility checker.</param>
    public void Initialize(
        IrregularMeshMapData mapData, 
        int startCellId, 
        IVisibilityChecker? visibilityChecker = null,
        IrregularMeshFogOfWar? fogOfWar = null)
    {
        _mapData = mapData;
        _visibilityChecker = visibilityChecker ?? new SimpleVisibilityChecker();
        _pathfinder = new Pathfinder(mapData);

        _movementController!.Initialize(mapData, startCellId);

        // Use provided fog of war or create our own
        _fogOfWar = fogOfWar ?? new IrregularMeshFogOfWar(mapData, _visibilityChecker);

        // Create ExplorationAI using the IMapData interface
        // Pass fog of war so navigation uses the same visibility state as rendering
        _explorationAI = new ExplorationAI(mapData, startCellId, _visibilityChecker, fogOfWar: _fogOfWar);

        // Subscribe to exploration events
        _explorationAI.PathUpdated += OnExplorationPathUpdated;
        _explorationAI.VisibilityUpdated += OnExplorationVisibilityUpdated;
        _explorationAI.EnemySpotted += OnExplorationEnemySpotted;
        _explorationAI.EnemyEncountered += OnExplorationEnemyEncountered;

        // Initial fog update from starting position
        _fogOfWar.UpdateVisibility(startCellId);
    }

    /// <summary>
    /// Start automatic exploration.
    /// </summary>
    public void StartExploration()
    {
        if (_explorationAI == null || _isExploring)
            return;

        _isExploring = true;
        ProcessNextExplorationStep();
    }

    /// <summary>
    /// Stop automatic exploration.
    /// </summary>
    public void StopExploration()
    {
        _isExploring = false;
    }

    /// <summary>
    /// Perform a single exploration step.
    /// </summary>
    /// <returns>True if a step was taken, false if exploration is complete.</returns>
    public bool StepExploration()
    {
        if (_explorationAI == null)
            return false;

        return _explorationAI.StepExploration();
    }

    /// <summary>
    /// Move to a specific cell along the current path.
    /// </summary>
    public async Task MoveToTargetAsync(int targetCellId)
    {
        if (_mapData == null || _pathfinder == null || _movementController == null)
            return;

        var path = _pathfinder.FindPath(CurrentCellId, targetCellId);
        if (path == null || path.Count == 0)
            return;

        _currentPath = path;
        PathUpdated?.Invoke(path);

        await _movementController.MoveAlongPathAsync(path);

        _currentPath = null;
        PathUpdated?.Invoke(Array.Empty<int>());
    }

    /// <summary>
    /// Get the current exploration mode.
    /// </summary>
    public ExplorationMode GetMode()
    {
        return _explorationAI?.CurrentMode ?? ExplorationMode.FrontierExploration;
    }

    /// <summary>
    /// Get the visual node for positioning sprites/characters.
    /// </summary>
    public Node2D GetVisualNode() => _movementController!;

    private async void ProcessNextExplorationStep()
    {
        // Guard against re-entrance
        if (_processingStep)
            return;

        if (!_isExploring || _explorationAI == null || _movementController == null)
            return;

        _processingStep = true;
        try
        {
            // Get the next step from exploration AI
            var stepped = _explorationAI.StepExploration();

            if (!stepped || _explorationAI.HasFinishedExploration)
            {
                _isExploring = false;
                ExplorationFinished?.Invoke();
                return;
            }

            // After StepExploration, the AI has already moved internally to the next cell.
            // We need to animate the visual movement to where the AI now is.
            var targetCell = _explorationAI.CurrentCellId;
            
            // Update path display (shows remaining path after current position)
            var remainingPath = _explorationAI.CurrentPath;
            if (remainingPath != null)
            {
                // Include current cell at start of displayed path
                _currentPath = new List<int> { targetCell };
                _currentPath.AddRange(remainingPath);
                PathUpdated?.Invoke(_currentPath);
            }

            // Animate to where the AI moved
            await _movementController.MoveToCellAsync(targetCell);

            // Wait before next step
            if (StepDelay > 0)
            {
                await ToSignal(GetTree().CreateTimer(StepDelay), SceneTreeTimer.SignalName.Timeout);
            }

            // Continue exploration if still active
            if (_isExploring && AutoExplore)
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

    private void OnMovementCompleted(int cellId, Vector2 worldPos)
    {
        PlayerMoved?.Invoke(cellId, worldPos);
    }

    private void OnPositionUpdated(Vector2 worldPos)
    {
        PositionUpdated?.Invoke(worldPos);
    }

    private void OnCellEntered(int cellId)
    {
        // Update fog of war from new position
        _fogOfWar?.UpdateVisibility(cellId);
    }

    private void OnExplorationPathUpdated()
    {
        if (_explorationAI?.CurrentPath != null)
        {
            PathUpdated?.Invoke(_explorationAI.CurrentPath.ToList());
        }
    }

    private void OnExplorationVisibilityUpdated(IReadOnlySet<int> seen, IReadOnlySet<int> visible)
    {
        VisibilityUpdated?.Invoke(seen, visible);
    }

    private void OnExplorationEnemySpotted(Vector2 enemyPos)
    {
        if (_mapData != null)
        {
            var cellId = _mapData.GetCellAtPosition(enemyPos);
            if (cellId.HasValue)
                EnemySpotted?.Invoke(cellId.Value);
        }
    }

    private void OnExplorationEnemyEncountered(Vector2 enemyPos)
    {
        // Stop exploration when entering combat
        _isExploring = false;

        if (_mapData != null)
        {
            var cellId = _mapData.GetCellAtPosition(enemyPos);
            if (cellId.HasValue)
                EnemyEncountered?.Invoke(cellId.Value);
        }
    }

    /// <summary>
    /// Reset the exploration AI's combat state after combat ends.
    /// Call this before calling StartExploration() to resume.
    /// </summary>
    public void ResetCombatState()
    {
        _explorationAI?.ResetCombatState();
    }
}
