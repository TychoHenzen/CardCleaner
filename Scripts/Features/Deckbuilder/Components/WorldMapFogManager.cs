using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Manages fog of war sprites for the world map.
/// Creates and updates fog sprites based on visibility state.
/// Implements IPassiveFogOfWar for receiving pre-computed visibility from game session.
/// </summary>
public class WorldMapFogManager : IPassiveFogOfWar
{
    private readonly Dictionary<Vector2I, Sprite2D> _fogSprites = new();
    private readonly Dictionary<int, FogState> _fogStates = new();
    private readonly HashSet<int> _currentlyVisible = new();
    private readonly HashSet<int> _seenCells = new();

    private ImageTexture? _fogTexture;
    private readonly int _tileSize;
    private readonly Node _parent;
    private Vector2I _mapSize;

    public event Action<IReadOnlySet<int>>? VisibilityChanged;

    public IReadOnlySet<int> CurrentlyVisibleCells => _currentlyVisible;
    public IReadOnlySet<int> SeenCells => _seenCells;

    public WorldMapFogManager(Node parent, int tileSize)
    {
        _parent = parent;
        _tileSize = tileSize;
    }

    /// <summary>
    /// Initialize fog of war for a map of the given size.
    /// </summary>
    public void Initialize(Vector2I mapSize)
    {
        Clear();
        _mapSize = mapSize;

        // Create black fog texture if not already created
        _fogTexture ??= CreateColorTexture(new Color(0, 0, 0, 1), _tileSize);

        // Create fog sprites for all tiles (90% opacity so map is barely visible)
        for (var y = 0; y < mapSize.Y; y++)
        for (var x = 0; x < mapSize.X; x++)
        {
            var position = new Vector2I(x, y);
            var cellId = GridPosToCellId(position);

            var sprite = new Sprite2D
            {
                Texture = _fogTexture,
                Position = new Vector2(x * _tileSize + _tileSize / 2, y * _tileSize + _tileSize / 2),
                ZIndex = 50,
                Modulate = new Color(1, 1, 1, 0.9f)
            };

            _parent.AddChild(sprite);
            _fogSprites[position] = sprite;
            _fogStates[cellId] = FogState.Hidden;
        }
    }

    #region IFogOfWar Implementation

    public FogState GetFogState(int cellId)
    {
        return _fogStates.TryGetValue(cellId, out var state) ? state : FogState.Hidden;
    }

    public bool IsVisible(int cellId) => GetFogState(cellId) == FogState.Visible;

    public bool HasBeenSeen(int cellId) => GetFogState(cellId) != FogState.Hidden;

    public void RevealAll()
    {
        var changedCells = new HashSet<int>();

        foreach (var (position, sprite) in _fogSprites)
        {
            sprite.Modulate = new Color(1, 1, 1, 0);

            var cellId = GridPosToCellId(position);
            if (_fogStates[cellId] != FogState.Visible)
            {
                _fogStates[cellId] = FogState.Visible;
                _seenCells.Add(cellId);
                _currentlyVisible.Add(cellId);
                changedCells.Add(cellId);
            }
        }

        if (changedCells.Count > 0)
            VisibilityChanged?.Invoke(changedCells);
    }

    public void Reset()
    {
        var changedCells = new HashSet<int>();

        foreach (var (position, sprite) in _fogSprites)
        {
            sprite.Modulate = new Color(1, 1, 1, 0.9f);

            var cellId = GridPosToCellId(position);
            if (_fogStates[cellId] != FogState.Hidden)
            {
                _fogStates[cellId] = FogState.Hidden;
                changedCells.Add(cellId);
            }
        }

        _currentlyVisible.Clear();
        _seenCells.Clear();

        if (changedCells.Count > 0)
            VisibilityChanged?.Invoke(changedCells);
    }

    #endregion

    #region IPassiveFogOfWar Implementation

    /// <summary>
    /// Update visibility based on externally computed seen and visible cell ID sets.
    /// </summary>
    public void UpdateVisibility(IReadOnlySet<int> seenCellIds, IReadOnlySet<int> visibleCellIds)
    {
        var changedCells = new HashSet<int>();

        foreach (var (position, sprite) in _fogSprites)
        {
            var cellId = GridPosToCellId(position);
            var previousState = _fogStates[cellId];
            FogState newState;

            if (visibleCellIds.Contains(cellId))
            {
                sprite.Modulate = new Color(1, 1, 1, 0);
                newState = FogState.Visible;
                _currentlyVisible.Add(cellId);
                _seenCells.Add(cellId);
            }
            else if (seenCellIds.Contains(cellId))
            {
                sprite.Modulate = new Color(1, 1, 1, 0.5f);
                newState = FogState.Revealed;
                _currentlyVisible.Remove(cellId);
                _seenCells.Add(cellId);
            }
            else
            {
                newState = FogState.Hidden;
                _currentlyVisible.Remove(cellId);
            }

            if (newState != previousState)
            {
                _fogStates[cellId] = newState;
                changedCells.Add(cellId);
            }
        }

        if (changedCells.Count > 0)
            VisibilityChanged?.Invoke(changedCells);
    }

    #endregion

    #region Legacy Vector2I API

    /// <summary>
    /// Update fog of war visibility based on seen and currently visible tiles (Vector2I version).
    /// </summary>
    public void UpdateVisibility(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        var seenCellIds = new HashSet<int>();
        var visibleCellIds = new HashSet<int>();

        foreach (var pos in seenTiles)
            seenCellIds.Add(GridPosToCellId(pos));

        foreach (var pos in currentlyVisibleTiles)
            visibleCellIds.Add(GridPosToCellId(pos));

        UpdateVisibility(seenCellIds, visibleCellIds);
    }

    #endregion

    /// <summary>
    /// Clear all fog sprites and state.
    /// </summary>
    public void Clear()
    {
        foreach (var sprite in _fogSprites.Values)
            sprite?.QueueFree();
        _fogSprites.Clear();
        _fogStates.Clear();
        _currentlyVisible.Clear();
        _seenCells.Clear();
    }

    /// <summary>
    /// Number of fog sprites currently managed.
    /// </summary>
    public int SpriteCount => _fogSprites.Count;

    private int GridPosToCellId(Vector2I pos) => pos.Y * _mapSize.X + pos.X;

    private static ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }
}
