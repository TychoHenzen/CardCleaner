using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Analyzes contiguous regions in a tile map using flood-fill.
/// Provides metrics for evaluating spatial coherence of generated maps.
/// </summary>
internal static class RegionAnalyzer
{
    /// <summary>
    /// Analyzes a tile map and returns region statistics.
    /// </summary>
    /// <param name="tileMap">2D array of tile IDs [row, col]</param>
    /// <returns>Metrics about contiguous regions in the map</returns>
    public static RegionMetrics Analyze(string[,] tileMap)
    {
        ArgumentNullException.ThrowIfNull(tileMap);

        var regionSizes = FindRegionSizes(tileMap);
        if (regionSizes.Count == 0)
            return new RegionMetrics(0, 0, 0, 0, 0, 0, 0);

        return Summarize(regionSizes);
    }

    /// <summary>
    /// Analyzes tile type distribution across the map.
    /// Returns percentage of each tile type and region counts.
    /// </summary>
    public static DistributionMetrics AnalyzeDistribution(string[,] tileMap)
    {
        ArgumentNullException.ThrowIfNull(tileMap);

        var tally = TallyTiles(tileMap);
        if (tally.TotalTiles == 0)
            return new DistributionMetrics(0, 0, []);

        return BuildDistribution(tally);
    }

    private static List<int> FindRegionSizes(string[,] tileMap)
    {
        var height = tileMap.GetLength(0);
        var width = tileMap.GetLength(1);
        var visited = new bool[height, width];
        var regionSizes = new List<int>();

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (visited[y, x] || string.IsNullOrEmpty(tileMap[y, x]))
                    continue;

                regionSizes.Add(FloodFill(tileMap, visited, x, y, tileMap[y, x]));
            }
        }

        return regionSizes;
    }

    private static RegionMetrics Summarize(List<int> regionSizes)
    {
        var minSize = int.MaxValue;
        var maxSize = 0;
        var tilesInLargeRegions = 0;
        var totalTiles = 0;

        foreach (var size in regionSizes)
        {
            totalTiles += size;
            if (size < minSize) minSize = size;
            if (size > maxSize) maxSize = size;
            if (size >= 30) tilesInLargeRegions += size;
        }

        var avgSize = (float)totalTiles / regionSizes.Count;
        var percentInLargeRegions = totalTiles > 0 ? (tilesInLargeRegions * 100.0f / totalTiles) : 0f;

        return new RegionMetrics(
            regionSizes.Count,
            minSize,
            maxSize,
            avgSize,
            percentInLargeRegions,
            tilesInLargeRegions,
            totalTiles
        );
    }

    private static TileTally TallyTiles(string[,] tileMap)
    {
        var height = tileMap.GetLength(0);
        var width = tileMap.GetLength(1);
        var tally = new TileTally();
        var visited = new bool[height, width];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var tile = tileMap[y, x];
                if (string.IsNullOrEmpty(tile))
                    continue;

                tally.RecordTile(tile);

                // Count regions via flood fill
                if (visited[y, x])
                    continue;

                FloodFill(tileMap, visited, x, y, tile);
                tally.RecordRegion(tile);
            }
        }

        return tally;
    }

    private static DistributionMetrics BuildDistribution(TileTally tally)
    {
        var distributions = tally.TileCounts
            .Select(kvp => new TileTypeDistribution(
                kvp.Key,
                kvp.Value,
                kvp.Value * 100f / tally.TotalTiles,
                tally.TileRegions.GetValueOrDefault(kvp.Key, 0)))
            .OrderByDescending(d => d.Percentage)
            .ToArray();

        return new DistributionMetrics(tally.TotalTiles, tally.TileCounts.Count, distributions);
    }

    private static int FloodFill(string[,] tileMap, bool[,] visited, int startX, int startY, string targetTile)
    {
        var height = tileMap.GetLength(0);
        var width = tileMap.GetLength(1);
        var queue = new Queue<Vector2I>();
        queue.Enqueue(new Vector2I(startX, startY));
        visited[startY, startX] = true;
        var count = 0;

        while (queue.Count > 0)
        {
            var pos = queue.Dequeue();
            count++;

            // Check 4-directional neighbors
            var neighbors = new[]
            {
                new Vector2I(pos.X - 1, pos.Y), // Left
                new Vector2I(pos.X + 1, pos.Y), // Right
                new Vector2I(pos.X, pos.Y - 1), // Up
                new Vector2I(pos.X, pos.Y + 1)  // Down
            };

            foreach (var neighbor in neighbors)
            {
                if (neighbor.X < 0 || neighbor.X >= width ||
                    neighbor.Y < 0 || neighbor.Y >= height)
                    continue;

                if (visited[neighbor.Y, neighbor.X])
                    continue;

                if (tileMap[neighbor.Y, neighbor.X] == targetTile)
                {
                    visited[neighbor.Y, neighbor.X] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        return count;
    }
}
