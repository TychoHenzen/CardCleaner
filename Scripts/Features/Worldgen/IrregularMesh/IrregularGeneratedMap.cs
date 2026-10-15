using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Wrapper that adapts the irregular mesh to the IGeneratedMap interface.
/// </summary>
public class IrregularGeneratedMap : IMeshGeneratedMap
{
    private readonly IIrregularMesh _mesh;
    private readonly IrregularMeshMapData _mapData;

    public IrregularGeneratedMap(IIrregularMesh mesh, IrregularMeshMapData mapData)
    {
        _mesh = mesh;
        _mapData = mapData;
    }

    /// <summary>
    /// Gets the underlying mesh for rendering.
    /// </summary>
    public IIrregularMesh RawMesh => _mesh;

    /// <summary>
    /// Gets the map data adapter.
    /// </summary>
    public IrregularMeshMapData MeshMapData => _mapData;

    /// <inheritdoc />
    public IMapData GetMapData() => _mapData;

    /// <inheritdoc />
    public int EnemyCount => _mapData.EnemySpawnCells.Count;

    /// <inheritdoc />
    public bool RemoveEnemyAt(int cellId)
    {
        return _mapData.RemoveEnemySpawn(cellId);
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
