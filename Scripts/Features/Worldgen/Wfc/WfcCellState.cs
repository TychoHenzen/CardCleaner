using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Represents the state of a single cell in the WFC grid.
/// Tracks the set of possible tile IDs that can be placed at this position.
/// </summary>
public class WfcCellState
{
    private readonly HashSet<string> _possibleTiles;

    /// <summary>
    /// Position of the anchor cell that reserved this cell (for multi-cell variants).
    /// Null if this cell is not reserved.
    /// </summary>
    public Vector2I? ReservedBy { get; private set; }

    /// <summary>
    /// Returns true if this cell has been reserved by a multi-cell variant.
    /// Reserved cells should not be selected for collapse or have their possibilities modified.
    /// </summary>
    public bool IsReserved => ReservedBy.HasValue;

    /// <summary>
    /// Creates a cell with the given set of possible tiles.
    /// </summary>
    public WfcCellState(IEnumerable<string> possibleTiles)
    {
        _possibleTiles = new HashSet<string>(possibleTiles);
    }

    /// <summary>
    /// Creates a copy of another cell state.
    /// </summary>
    public WfcCellState(WfcCellState other)
    {
        _possibleTiles = new HashSet<string>(other._possibleTiles);
        ReservedBy = other.ReservedBy;
    }

    /// <summary>
    /// Calculates weighted Shannon entropy based on tile probabilities.
    /// Lower entropy = clearer winner (more certainty about which tile to pick).
    /// </summary>
    /// <param name="weights">Tile ID to weight mapping. Missing tiles default to 1.0.</param>
    /// <returns>Shannon entropy value. 0 for collapsed/contradiction, float.MaxValue for zero total weight.</returns>
    public float GetWeightedEntropy(IReadOnlyDictionary<string, float> weights)
    {
        if (_possibleTiles.Count <= 1)
            return 0f; // Already collapsed or contradiction

        var totalWeight = 0f;
        foreach (var tileId in _possibleTiles)
        {
            totalWeight += weights.TryGetValue(tileId, out var w) ? w : 1f;
        }

        if (totalWeight <= 0f)
            return float.MaxValue;

        var entropy = 0f;
        foreach (var tileId in _possibleTiles)
        {
            var weight = weights.TryGetValue(tileId, out var w) ? w : 1f;
            if (weight <= 0f) continue;

            var p = weight / totalWeight;
            entropy -= p * Mathf.Log(p);
        }

        return entropy;
    }

    /// <summary>
    /// Returns true if this cell has collapsed to a single tile.
    /// </summary>
    public bool IsCollapsed() => _possibleTiles.Count == 1;

    /// <summary>
    /// Returns true if this cell should be excluded from WFC cell selection.
    /// This includes collapsed cells (already decided) and reserved cells (occupied by multi-cell variants).
    /// </summary>
    public bool IsExcludedFromSelection() => IsCollapsed() || IsReserved;

    /// <summary>
    /// Returns true if this cell has no valid options (contradiction state).
    /// </summary>
    public bool IsContradiction() => _possibleTiles.Count == 0;

    /// <summary>
    /// Gets the collapsed tile ID. Throws if not collapsed.
    /// </summary>
    public string GetCollapsedTile()
    {
        if (!IsCollapsed())
            throw new InvalidOperationException(
                $"Cannot get collapsed tile: cell has {_possibleTiles.Count} options");

        foreach (var tile in _possibleTiles)
            return tile;

        throw new InvalidOperationException("Cell is empty");
    }

    /// <summary>
    /// Removes a tile from the possible set.
    /// Returns true if the tile was present and removed.
    /// </summary>
    public bool RemoveTile(string tileId) => _possibleTiles.Remove(tileId);

    /// <summary>
    /// Checks if a tile is still a valid option.
    /// </summary>
    public bool ContainsTile(string tileId) => _possibleTiles.Contains(tileId);

    /// <summary>
    /// Gets all currently possible tiles (read-only view).
    /// </summary>
    public IReadOnlyCollection<string> GetPossibleTiles() => _possibleTiles;

    /// <summary>
    /// Collapses this cell to a single tile.
    /// Removes all other options.
    /// </summary>
    public void CollapseTo(string tileId)
    {
        if (!_possibleTiles.Contains(tileId))
            throw new ArgumentException($"Cannot collapse to '{tileId}': not in possible set");

        _possibleTiles.Clear();
        _possibleTiles.Add(tileId);
    }

    /// <summary>
    /// Retains only tiles that are in the given set.
    /// Returns true if any tiles were removed.
    /// </summary>
    public bool IntersectWith(IEnumerable<string> validTiles)
    {
        var validSet = validTiles is HashSet<string> hs ? hs : new HashSet<string>(validTiles);
        var originalCount = _possibleTiles.Count;
        _possibleTiles.IntersectWith(validSet);
        return _possibleTiles.Count < originalCount;
    }

    /// <summary>
    /// Reserves this cell for a multi-cell variant anchored at the given position.
    /// Reserved cells are excluded from WFC collapse and constraint propagation.
    /// </summary>
    /// <param name="anchorPosition">The grid position of the cell that placed the multi-cell variant.</param>
    public void Reserve(Vector2I anchorPosition)
    {
        ReservedBy = anchorPosition;
    }

    /// <summary>
    /// Clears the reservation on this cell, making it available for WFC processing again.
    /// </summary>
    public void ClearReservation()
    {
        ReservedBy = null;
    }
}
