using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Wrapper that adapts SimpleMapData to the IGeneratedMap interface.
/// </summary>
public class SimpleGeneratedMap : IGeneratedMap
{
    private readonly SimpleMapData _mapData;
    private readonly RegularGridMapData _mapDataAdapter;

    public SimpleGeneratedMap(SimpleMapData mapData)
    {
        _mapData = mapData;
        _mapDataAdapter = new RegularGridMapData(mapData);
    }

    /// <summary>
    /// Gets the underlying SimpleMapData for rendering.
    /// </summary>
    public SimpleMapData RawMapData => _mapData;

    /// <inheritdoc />
    public IMapData GetMapData() => _mapDataAdapter;

    /// <inheritdoc />
    public int EnemyCount => _mapData.EnemyPositions.Count;

    /// <inheritdoc />
    public bool RemoveEnemyAt(int cellId)
    {
        var gridPos = _mapDataAdapter.CellIdToPosition(cellId);
        return _mapData.EnemyPositions.Remove(gridPos);
    }

    /// <inheritdoc />
    public Vector2 PlayerStartPosition => _mapDataAdapter.GridToWorld(_mapData.PlayerStart);
}
