using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Open set, cost table and parent links for a single A* search over cell IDs.
/// </summary>
internal sealed class AStarSearchState
{
    private readonly PriorityQueue<int, float> _openSet = new();
    private readonly Dictionary<int, int> _cameFrom = new();
    private readonly Dictionary<int, float> _gScore = new();
    private readonly HashSet<int> _inOpenSet = new();

    internal AStarSearchState(int startCell, float startEstimate)
    {
        _gScore[startCell] = 0;
        _openSet.Enqueue(startCell, startEstimate);
        _inOpenSet.Add(startCell);
    }

    internal bool TryDequeue(out int current)
    {
        if (_openSet.Count == 0)
        {
            current = default;
            return false;
        }

        current = _openSet.Dequeue();
        _inOpenSet.Remove(current);
        return true;
    }

    /// <summary>
    /// Records <paramref name="current"/> as the parent of <paramref name="neighbor"/> when the new
    /// route is cheaper than any known one.
    /// </summary>
    internal bool TryImprovePath(int current, int neighbor, float movementCost, out float newCost)
    {
        newCost = _gScore[current] + movementCost;
        if (_gScore.TryGetValue(neighbor, out var existing) && newCost >= existing)
            return false;

        _cameFrom[neighbor] = current;
        _gScore[neighbor] = newCost;
        return true;
    }

    internal void EnqueueIfAbsent(int cell, float estimate)
    {
        if (!_inOpenSet.Add(cell))
            return;

        _openSet.Enqueue(cell, estimate);
    }

    internal List<int> ReconstructPath(int current)
    {
        var path = new List<int> { current };

        while (_cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Insert(0, current);
        }

        return path;
    }
}
