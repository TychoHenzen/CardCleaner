using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.FrontierExploration;

/// <summary>
/// Finds connected blobs of unvisited cells reachable from a starting cell.
/// Uses BFS through visited cells for walking distance, then flood fill for blob size.
/// </summary>
internal sealed class UnvisitedBlobFinder
{
    private readonly IMapData _mapData;
    private readonly IReadOnlySet<int> _visitedCells;

    internal UnvisitedBlobFinder(IMapData mapData, IReadOnlySet<int> visitedCells)
    {
        _mapData = mapData;
        _visitedCells = visitedCells;
    }

    internal List<UnvisitedBlob> Find(int currentCellId)
    {
        var blobs = new List<UnvisitedBlob>();
        var blobAssigned = new HashSet<int>();

        foreach (var entry in FindFrontierCells(currentCellId))
        {
            if (blobAssigned.Contains(entry.CellId))
                continue;

            blobs.Add(new UnvisitedBlob
            {
                EntryCellId = entry.CellId,
                Size = FloodFillBlob(entry.CellId, blobAssigned),
                WalkingDistance = entry.WalkingDistance
            });
        }

        return blobs;
    }

    private bool IsWalkable(int cellId)
    {
        return _mapData.IsValidCell(cellId) && _mapData.IsPassable(cellId);
    }

    /// <summary>
    /// BFS through visited cells, recording every unvisited cell touched with its walking distance.
    /// </summary>
    private List<FrontierCell> FindFrontierCells(int currentCellId)
    {
        var bfsVisited = new HashSet<int> { currentCellId };
        var queue = new Queue<FrontierCell>();
        queue.Enqueue(new FrontierCell(currentCellId, 0));
        var frontier = new List<FrontierCell>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in _mapData.GetAdjacentCells(current.CellId))
            {
                if (bfsVisited.Contains(neighbor) || !IsWalkable(neighbor))
                    continue;

                bfsVisited.Add(neighbor);
                var reached = new FrontierCell(neighbor, current.WalkingDistance + 1);

                if (_visitedCells.Contains(neighbor))
                    queue.Enqueue(reached);
                else
                    frontier.Add(reached);
            }
        }

        return frontier;
    }

    /// <summary>
    /// Flood fills all connected unvisited cells from the entry cell and returns the blob size.
    /// </summary>
    private int FloodFillBlob(int entryCell, HashSet<int> blobAssigned)
    {
        var blobSize = 0;
        var floodQueue = new Queue<int>();
        floodQueue.Enqueue(entryCell);
        blobAssigned.Add(entryCell);

        while (floodQueue.Count > 0)
        {
            var cell = floodQueue.Dequeue();
            blobSize++;

            foreach (var neighbor in _mapData.GetAdjacentCells(cell))
            {
                if (blobAssigned.Contains(neighbor) || !IsWalkable(neighbor) || _visitedCells.Contains(neighbor))
                    continue;

                blobAssigned.Add(neighbor);
                floodQueue.Enqueue(neighbor);
            }
        }

        return blobSize;
    }
}
