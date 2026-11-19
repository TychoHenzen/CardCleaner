using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Models;

/// <summary>
///     Groups points that lie close together: two points within the link distance are in one group, and so is every
///     point linked to them through a chain of such links (single linkage). Points are bucketed into a grid of cells as
///     wide as the link distance, so only the points of neighbouring cells are compared.
/// </summary>
public static class CardClusters
{
    /// <summary>
    ///     The groups of <paramref name="points" /> with more than <paramref name="most" /> members, linked within
    ///     <paramref name="link" />. Each group lists its point indices in ascending order, and the groups come in the
    ///     order of their first index, so the same points always give the same groups.
    /// </summary>
    public static List<List<int>> LargerThan(IReadOnlyList<Vector3> points, float link, int most)
    {
        if (link <= 0f)
            throw new ArgumentOutOfRangeException(nameof(link), link, "The link distance must be positive");

        var parents = Enumerable.Range(0, points.Count).ToArray();
        var cells = new Dictionary<(int, int, int), List<int>>();
        for (var i = 0; i < points.Count; i++)
        {
            var cell = CellOf(points[i], link);
            foreach (var neighbour in Neighbours(cell))
            {
                if (!cells.TryGetValue(neighbour, out var others))
                    continue;

                foreach (var other in others.Where(other => points[i].DistanceTo(points[other]) <= link))
                    Join(parents, i, other);
            }

            if (!cells.TryGetValue(cell, out var members))
                cells[cell] = members = new List<int>();
            members.Add(i);
        }

        return Enumerable.Range(0, points.Count)
            .GroupBy(i => Root(parents, i))
            .Where(group => group.Count() > most)
            .Select(group => group.OrderBy(i => i).ToList())
            .OrderBy(group => group[0])
            .ToList();
    }

    private static (int, int, int) CellOf(Vector3 point, float size) =>
        ((int)MathF.Floor(point.X / size), (int)MathF.Floor(point.Y / size), (int)MathF.Floor(point.Z / size));

    private static IEnumerable<(int, int, int)> Neighbours((int X, int Y, int Z) cell)
    {
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        for (var z = -1; z <= 1; z++)
            yield return (cell.X + x, cell.Y + y, cell.Z + z);
    }

    private static int Root(int[] parents, int i)
    {
        while (parents[i] != i)
        {
            parents[i] = parents[parents[i]];
            i = parents[i];
        }

        return i;
    }

    private static void Join(int[] parents, int a, int b)
    {
        var rootA = Root(parents, a);
        var rootB = Root(parents, b);
        if (rootA != rootB)
            parents[Math.Max(rootA, rootB)] = Math.Min(rootA, rootB);
    }
}
