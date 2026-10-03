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
    private readonly Dictionary<int, float> _bestEstimate = new();

    internal AStarSearchState(int startCell, float startEstimate)
    {
        _gScore[startCell] = 0;
        Enqueue(startCell, startEstimate);
    }

    /// <summary>
    /// Dequeues the open cell with the lowest estimate, skipping entries superseded by a cheaper route.
    /// </summary>
    internal bool TryDequeue(out int current)
    {
        while (_openSet.TryDequeue(out current, out var estimate))
        {
            if (estimate <= _bestEstimate[current])
                return true;
        }

        return false;
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

    /// <summary>
    /// Queues <paramref name="cell"/> with a new estimate. An earlier entry for the same cell becomes stale.
    /// </summary>
    internal void Enqueue(int cell, float estimate)
    {
        _bestEstimate[cell] = estimate;
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
