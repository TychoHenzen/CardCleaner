using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Path finding over the built cells of the workshop grid, for tests that need a route a player body can
/// follow. A floor cell is clear when every cell within the clearance square is floor, so a path of clear
/// cells keeps the body's radius away from every wall. The workshop room just inside the doorway counts as
/// floor, because the grid's own cells stop at the threshold.
/// </summary>
public sealed class WorkshopGridPaths
{
    public const int FloorItem = 0;
    public const int WallItem = 1;
    public const int FloorLayer = -1;

    // Cell range of the workshop room strip in front of the doorway, in grid cells (x up to the threshold).
    private const int ThresholdCellX = 30;
    private const int RoomStripWidthCells = 10;
    private const int DoorwayHalfWidthCells = 10;

    private static readonly Vector2I[] Steps = [Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up];

    private readonly HashSet<Vector2I> _clear;
    private readonly WorkshopGrid _grid;

    public WorkshopGridPaths(WorkshopGrid grid, int clearanceCells)
    {
        _grid = grid;
        Floor = grid.GetUsedCellsByItem(FloorItem)
            .Where(c => c.Y == FloorLayer)
            .Select(c => new Vector2I(c.X, c.Z))
            .ToHashSet();

        var floorWithRoom = new HashSet<Vector2I>(Floor);
        for (var x = ThresholdCellX - RoomStripWidthCells; x < ThresholdCellX; x++)
        for (var z = -DoorwayHalfWidthCells; z < DoorwayHalfWidthCells; z++)
            floorWithRoom.Add(new Vector2I(x, z));

        _clear = Floor.Where(c => IsClear(floorWithRoom, c, clearanceCells)).ToHashSet();
    }

    public HashSet<Vector2I> Floor { get; }

    public IReadOnlySet<Vector2I> Clear => _clear;

    /// <summary>
    ///     Floor cells with no clear cell within <paramref name="clearanceCells" />: places that are walkable on
    ///     paper but too tight for the player body, such as a hallway narrower than the capsule.
    /// </summary>
    public int CountFloorCellsTooTight(int clearanceCells)
    {
        var near = new HashSet<Vector2I>();
        foreach (var cell in _clear)
        for (var dx = -clearanceCells; dx <= clearanceCells; dx++)
        for (var dz = -clearanceCells; dz <= clearanceCells; dz++)
            near.Add(new Vector2I(cell.X + dx, cell.Y + dz));

        return Floor.Count(c => !near.Contains(c));
    }

    public Vector2I CellOf(Vector3 world)
    {
        var cell = _grid.WorldToCell(world);
        return new Vector2I(cell.X, cell.Z);
    }

    /// <summary>The cell of a position in the grid node's own space, for grids that are not in a tree.</summary>
    public Vector2I CellOfLocal(Vector3 local)
    {
        var cell = _grid.Coordinates.LocalToCell(local);
        return new Vector2I(cell.X, cell.Z);
    }

    public Vector2 WorldXz(Vector2I cell)
    {
        var world = _grid.CellToWorld(new Vector3I(cell.X, 0, cell.Y));
        return new Vector2(world.X, world.Z);
    }

    public HashSet<Vector2I> ReachableFrom(Vector2I start)
    {
        var seen = new HashSet<Vector2I> { start };
        var pending = new Queue<Vector2I>([start]);
        while (pending.Count > 0)
        {
            var cell = pending.Dequeue();
            foreach (var step in Steps)
                if (_clear.Contains(cell + step) && seen.Add(cell + step))
                    pending.Enqueue(cell + step);
        }

        return seen;
    }

    /// <summary>Shortest 4-connected path of clear cells, or an empty list when there is none.</summary>
    public List<Vector2I> ShortestPath(Vector2I from, Vector2I to)
    {
        var cameFrom = new Dictionary<Vector2I, Vector2I> { [from] = from };
        var pending = new Queue<Vector2I>([from]);
        while (pending.Count > 0 && !cameFrom.ContainsKey(to))
        {
            var cell = pending.Dequeue();
            foreach (var step in Steps)
            {
                var next = cell + step;
                if (!_clear.Contains(next) || cameFrom.ContainsKey(next))
                    continue;
                cameFrom[next] = cell;
                pending.Enqueue(next);
            }
        }

        var path = new List<Vector2I>();
        if (!cameFrom.ContainsKey(to))
            return path;

        for (var cell = to; cell != from; cell = cameFrom[cell])
            path.Add(cell);
        path.Add(from);
        path.Reverse();
        return path;
    }

    /// <summary>Straight-line waypoints along <paramref name="path" /> that never leave the clear cells.</summary>
    public List<Vector2I> StraightenPath(List<Vector2I> path)
    {
        var waypoints = new List<Vector2I>();
        var anchor = 0;
        while (anchor < path.Count - 1)
        {
            var farthest = anchor + 1;
            for (var i = path.Count - 1; i > anchor + 1; i--)
            {
                if (!HasClearLine(path[anchor], path[i]))
                    continue;
                farthest = i;
                break;
            }

            waypoints.Add(path[farthest]);
            anchor = farthest;
        }

        return waypoints;
    }

    private bool HasClearLine(Vector2I a, Vector2I b)
    {
        var steps = Mathf.CeilToInt(((Vector2)(b - a)).Length() * 2f);
        for (var i = 0; i <= steps; i++)
        {
            var point = ((Vector2)a).Lerp(b, i / (float)steps);
            if (!_clear.Contains(new Vector2I(Mathf.RoundToInt(point.X), Mathf.RoundToInt(point.Y))))
                return false;
        }

        return true;
    }

    private static bool IsClear(HashSet<Vector2I> floor, Vector2I cell, int clearance)
    {
        for (var dx = -clearance; dx <= clearance; dx++)
        for (var dz = -clearance; dz <= clearance; dz++)
            if (!floor.Contains(new Vector2I(cell.X + dx, cell.Y + dz)))
                return false;
        return true;
    }
}
