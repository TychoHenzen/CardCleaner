using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Front door for the map data of an irregular mesh: wraps the mesh, places the player start and the enemies, and
/// returns the remove-enemy callback the map uses when an enemy is defeated.
/// </summary>
internal static class IrregularMaps
{
    internal static (IMapData Map, MapPlacement Placement, Func<int, bool> RemoveEnemy) Populated(
        IIrregularMesh mesh,
        float worldScale,
        IRandom rng)
    {
        var data = new IrregularMeshMapData(mesh) { WorldScale = worldScale };
        var placement = MapPopulation.Place(data, rng);
        data.SetPlayerStart(placement.PlayerStartCell);
        foreach (var enemyCell in placement.EnemyCells)
            data.AddEnemySpawn(enemyCell);
        return (data, placement, data.RemoveEnemySpawn);
    }
}
