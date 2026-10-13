using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// The one rule that places the player start and the enemies on a generated map, for every map kind: a random
/// passable start, then 2-3 enemies on passable cells at least <see cref="MinEnemyDistance"/> world units from the
/// start. Stateless: every random draw comes from the caller's generator, so the same map and seed give the same
/// placement.
/// </summary>
public static class MapPopulation
{
    /// <summary>World units per cell on both map kinds: a grid tile and an irregular mesh unit.</summary>
    public const float CellWorldSize = 16f;

    /// <summary>
    /// Minimum world distance from the start to an enemy: 12.5 cells, the irregular map's rule, on both map kinds.
    /// </summary>
    public const float MinEnemyDistance = 12.5f * CellWorldSize;

    public const int MinEnemies = 2;

    public const int MaxEnemies = 3;

    /// <summary>
    /// Picks the start and the enemy cells. A map without a passable cell starts on cell 0 with no enemies.
    /// </summary>
    public static MapPlacement Place(IMapData map, RandomNumberGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(rng);

        var passable = PassableCells(map);
        if (passable.Count == 0)
        {
            ILog.Error("[MapPopulation] The map has no passable cell; the player starts on cell 0 with no enemies");
            return new MapPlacement(0, Array.Empty<int>());
        }

        var start = passable[rng.RandiRange(0, passable.Count - 1)];
        var (candidates, farCount) = EnemyCandidates(map, passable, start, rng);
        if (candidates.Count == 0)
            return new MapPlacement(start, Array.Empty<int>());

        // Nearer cells only top the enemies up to MinEnemies; an enemy beyond that needs a far cell.
        var fewest = Math.Min(MinEnemies, candidates.Count);
        var most = Math.Min(Math.Min(MaxEnemies, candidates.Count), Math.Max(MinEnemies, farCount));
        var count = rng.RandiRange(fewest, most);
        return new MapPlacement(start, candidates.GetRange(0, count));
    }

    private static List<int> PassableCells(IMapData map)
    {
        var cells = new List<int>();
        for (var cell = 0; cell < map.CellCount; cell++)
        {
            if (map.IsPassable(cell))
                cells.Add(cell);
        }

        return cells;
    }

    // Every passable cell but the start, in shuffled order with the far cells first, so the enemies take far cells
    // while there are enough of them.
    // ASSUMPTION: a map with fewer far cells than MinEnemies (a small map) tops up from the nearer passable cells, as
    // the irregular map's rule fell back to them; otherwise the session would have fewer than 2 enemies to fight.
    private static (List<int> Cells, int FarCount) EnemyCandidates(
        IMapData map,
        List<int> passable,
        int start,
        RandomNumberGenerator rng)
    {
        var startCenter = map.GetCellCenter(start);
        var far = new List<int>();
        var near = new List<int>();
        foreach (var cell in passable)
        {
            if (cell == start)
                continue;

            if (map.GetCellCenter(cell).DistanceTo(startCenter) >= MinEnemyDistance)
                far.Add(cell);
            else
                near.Add(cell);
        }

        Shuffle(far, rng);
        Shuffle(near, rng);
        var farCount = far.Count;
        far.AddRange(near);
        return (far, farCount);
    }

    private static void Shuffle(List<int> cells, RandomNumberGenerator rng)
    {
        for (var i = cells.Count - 1; i > 0; i--)
        {
            var j = rng.RandiRange(0, i);
            (cells[i], cells[j]) = (cells[j], cells[i]);
        }
    }
}
