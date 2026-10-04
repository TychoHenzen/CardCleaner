using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Exploration;
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

    private ExplorationStepLoop? _stepLoop;
    private readonly ExplorationPathDisplay _pathDisplay = new();

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
    public bool IsExploring => _stepLoop?.IsExploring ?? false;

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
    public IReadOnlyList<Vector2> CurrentPathPositions => _pathDisplay.ToWorldPositions(_mapData);

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

        _movementController.MovementCompleted += (cellId, worldPos) => PlayerMoved?.Invoke(cellId, worldPos);
        _movementController.CellEntered += cellId => _fogOfWar?.UpdateVisibility(cellId); // fog follows the new cell
        _movementController.PositionUpdated += worldPos => PositionUpdated?.Invoke(worldPos);
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
        var aiEvents = new ExplorationAiEventTranslator(_explorationAI, mapData);
        aiEvents.PathUpdated += path => PathUpdated?.Invoke(path);
        aiEvents.VisibilityUpdated += (seen, visible) => VisibilityUpdated?.Invoke(seen, visible);
        aiEvents.EnemySpotted += cellId => EnemySpotted?.Invoke(cellId);
        aiEvents.EnemyEncountered += OnAiEnemyEncountered;

        _stepLoop = new ExplorationStepLoop(
            _explorationAI,
            _movementController,
            _pathDisplay,
            () => StepDelay > 0 ? ToSignal(GetTree().CreateTimer(StepDelay), SceneTreeTimer.SignalName.Timeout) : null,
            () => AutoExplore);
        _stepLoop.PathUpdated += path => PathUpdated?.Invoke(path);
        _stepLoop.Finished += () => ExplorationFinished?.Invoke();

        // Initial fog update from starting position
        _fogOfWar.UpdateVisibility(startCellId);
    }

    /// <summary>
    /// Start automatic exploration.
    /// </summary>
    public void StartExploration()
    {
        if (_stepLoop == null)
            return;

        _stepLoop.Start();
    }

    /// <summary>
    /// Stop automatic exploration.
    /// </summary>
    public void StopExploration()
    {
        if (_stepLoop != null)
            _stepLoop.IsExploring = false;
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

        _pathDisplay.Show(path);
        PathUpdated?.Invoke(path);

        await _movementController.MoveAlongPathAsync(path);

        _pathDisplay.Clear();
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

    private void OnAiEnemyEncountered(int? cellId)
    {
        // Stop exploration when entering combat
        StopExploration();

        if (cellId.HasValue)
            EnemyEncountered?.Invoke(cellId.Value);
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
