using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Workshop.Components;
using CardCleaner.Scripts.Features.Workshop.Models;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Path finding over the workshop grid, for tests that need a route a player body can follow. Floor is every
/// cell of the warehouse square that holds no baked wall, minus the footprints of any solid props passed in.
/// A floor cell is clear when every cell within the clearance square is floor, so a path of clear cells keeps
/// the body's radius away from every wall and prop.
/// </summary>
public sealed class WorkshopGridPaths
{
    private static readonly Vector2I[] Steps = [Vector2I.Right, Vector2I.Left, Vector2I.Down, Vector2I.Up];

    private readonly HashSet<Vector2I> _clear;
    private readonly HashSet<Vector2I> _floor;
    private readonly WorkshopGrid _grid;

    /// <param name="grid">The baked grid.</param>
    /// <param name="clearanceCells">Cells kept free on every side of a clear cell.</param>
    /// <param name="obstacles">World-space boxes of solid props standing on the floor.</param>
    public WorkshopGridPaths(WorkshopGrid grid, int clearanceCells, IEnumerable<Aabb>? obstacles = null)
    {
        _grid = grid;
        var walls = grid.GetUsedCellsByItem(WorkshopHallLayout.WallItem)
            .Select(c => new Vector2I(c.X, c.Z))
            .ToHashSet();
        _floor = WorkshopHallLayout.AllCells.Where(c => !walls.Contains(c)).ToHashSet();
        foreach (var box in obstacles ?? [])
            _floor.ExceptWith(CellsUnder(box));

        _clear = _floor.Where(c => IsClear(_floor, c, clearanceCells)).ToHashSet();
    }

    /// <summary>
    ///     World boxes of every solid body under <paramref name="root" /> except the grid's own floor and ceiling:
    ///     the props a walking player must go around.
    /// </summary>
    public static List<Aabb> SolidProps(Node root, WorkshopGrid grid)
    {
        return root.FindChildren("*", nameof(CollisionShape3D), true, false)
            .OfType<CollisionShape3D>()
            .Where(shape => shape.GetParent() is StaticBody3D body && !grid.IsAncestorOf(body) && shape.Shape != null)
            .Select(shape => shape.GlobalTransform * ShapeBounds(shape.Shape))
            .ToList();
    }

    private static Aabb ShapeBounds(Shape3D shape) =>
        shape is BoxShape3D box ? new Aabb(-box.Size / 2f, box.Size) : shape.GetDebugMesh().GetAabb();

    public IReadOnlySet<Vector2I> Floor => _floor;

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

        return _floor.Count(c => !near.Contains(c));
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

    private IEnumerable<Vector2I> CellsUnder(Aabb box)
    {
        var low = CellOf(box.Position);
        var high = CellOf(box.End);
        for (var x = low.X; x <= high.X; x++)
        for (var z = low.Y; z <= high.Y; z++)
            yield return new Vector2I(x, z);
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
