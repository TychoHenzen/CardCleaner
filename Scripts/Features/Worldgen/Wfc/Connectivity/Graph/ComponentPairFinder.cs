using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Finds the nearest nodes between disconnected components by Manhattan distance.
/// </summary>
internal static class ComponentPairFinder
{
    /// <summary>
    /// Finds the closest pair of nodes across all component pairs.
    /// Ties keep the first pair found. Requires at least two components.
    /// </summary>
    internal static ClosestPair FindOverallClosest(List<HashSet<Vector2I>> components)
    {
        var best = new ClosestPair(default, default, int.MaxValue);

        for (var i = 0; i < components.Count - 1; i++)
        {
            for (var j = i + 1; j < components.Count; j++)
            {
                var candidate = FindClosestBetween(components[i], components[j]);
                if (candidate.Distance < best.Distance)
                    best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Finds the closest pair of nodes for every pair of components.
    /// </summary>
    internal static List<ClosestPair> FindClosestPerComponentPair(List<HashSet<Vector2I>> components)
    {
        var pairs = new List<ClosestPair>();

        for (var i = 0; i < components.Count - 1; i++)
        {
            for (var j = i + 1; j < components.Count; j++)
            {
                pairs.Add(FindClosestBetween(components[i], components[j]));
            }
        }

        return pairs;
    }

    private static ClosestPair FindClosestBetween(HashSet<Vector2I> first, HashSet<Vector2I> second)
    {
        var best = new ClosestPair(default, default, int.MaxValue);

        foreach (var node1 in first)
        {
            foreach (var node2 in second)
            {
                var dist = ManhattanDistance(node1, node2);
                if (dist < best.Distance)
                    best = new ClosestPair(node1, node2, dist);
            }
        }

        return best;
    }

    private static int ManhattanDistance(Vector2I a, Vector2I b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
