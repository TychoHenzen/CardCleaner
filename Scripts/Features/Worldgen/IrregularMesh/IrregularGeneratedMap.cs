using System;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Wrapper that adapts the irregular mesh to the IGeneratedMap interface.
/// </summary>
public class IrregularGeneratedMap : IMeshGeneratedMap
{
    private readonly IIrregularMesh _mesh;
    private readonly IMapData _mapData;
    private readonly float _worldScale;
    private readonly Func<int, bool> _removeEnemy;

    public IrregularGeneratedMap(IIrregularMesh mesh, IMapData mapData, float worldScale, Func<int, bool> removeEnemy)
    {
        _mesh = mesh;
        _mapData = mapData;
        _worldScale = worldScale;
        _removeEnemy = removeEnemy;
    }

    /// <summary>
    /// Gets the underlying mesh for rendering.
    /// </summary>
    public IIrregularMesh RawMesh => _mesh;

    /// <summary>
    /// Gets the map data adapter.
    /// </summary>
    public IMapData MeshMapData => _mapData;

    /// <inheritdoc />
    public float WorldScale => _worldScale;

    /// <inheritdoc />
    public IMapData GetMapData() => _mapData;

    /// <inheritdoc />
    public int EnemyCount => _mapData.EnemySpawnCells.Count;

    /// <inheritdoc />
    public bool RemoveEnemyAt(int cellId)
    {
        return _removeEnemy(cellId);
    }

    /// <inheritdoc />
    public Vector2 PlayerStartPosition
    {
        get
        {
            var startCell = _mapData.PlayerStartCell;
            return startCell.HasValue
                ? _mapData.GetCellCenter(startCell.Value)
                : Vector2.Zero;
        }
    }
}
