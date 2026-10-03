using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.CellQueries;

/// <summary>
/// Searches an IMapData for passable cells.
/// </summary>
internal static class PassableCellFinder
{
    /// <summary>
    /// Picks one passable cell using a seeded random choice, or null when there is none.
    /// </summary>
    internal static int? FindRandom(IMapData mapData, int seed)
    {
        var passableCells = new List<int>();
        for (int i = 0; i < mapData.CellCount; i++)
        {
            if (mapData.IsPassable(i))
                passableCells.Add(i);
        }

        if (passableCells.Count == 0)
            return null;

        var random = new System.Random(seed);
        return passableCells[random.Next(passableCells.Count)];
    }

    /// <summary>
    /// Finds the passable cell whose centroid is closest to a position in mesh coordinates.
    /// </summary>
    internal static int? FindNearest(IMapData mapData, IrregularMesh mesh, Vector2 meshPos)
    {
        int? nearestCell = null;
        float nearestDist = float.MaxValue;

        for (int i = 0; i < mesh.Quads.Count; i++)
        {
            if (!mapData.IsPassable(i))
                continue;

            float dist = mesh.Quads[i].Centroid.DistanceSquaredTo(meshPos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestCell = i;
            }
        }

        return nearestCell;
    }
}
