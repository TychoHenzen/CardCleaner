using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Exploration;

/// <summary>
/// Holds the path currently shown for exploration and converts it to world positions.
/// </summary>
internal sealed class ExplorationPathDisplay
{
    private List<int>? _currentPath;

    /// <summary>
    /// Replaces the displayed path.
    /// </summary>
    internal void Show(List<int> path)
    {
        _currentPath = path;
    }

    /// <summary>
    /// Shows the current cell followed by the remaining path and returns the displayed path.
    /// </summary>
    internal List<int> ShowFromCell(int currentCell, IEnumerable<int> remainingPath)
    {
        _currentPath = new List<int> { currentCell };
        _currentPath.AddRange(remainingPath);
        return _currentPath;
    }

    internal void Clear()
    {
        _currentPath = null;
    }

    internal IReadOnlyList<Vector2> ToWorldPositions(IMapData? mapData)
    {
        if (_currentPath == null || mapData == null)
            return Array.Empty<Vector2>();

        return _currentPath
            .Select(id => mapData.GetCellCenter(id))
            .ToList();
    }
}
