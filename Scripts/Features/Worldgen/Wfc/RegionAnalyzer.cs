using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Analyzes contiguous regions in a tile map using flood-fill.
/// Provides metrics for evaluating spatial coherence of generated maps.
/// </summary>
public static class RegionAnalyzer
{
    /// <summary>
    /// Analyzes a tile map and returns region statistics.
    /// </summary>
    /// <param name="tileMap">2D array of tile IDs [row, col]</param>
    /// <returns>Metrics about contiguous regions in the map</returns>
    public static RegionMetrics Analyze(string[,] tileMap)
    {
        if (tileMap == null)
            throw new ArgumentNullException(nameof(tileMap));

        var height = tileMap.GetLength(0);
        var width = tileMap.GetLength(1);

        if (height == 0 || width == 0)
            return new RegionMetrics(0, 0, 0, 0, 0, 0, 0);

        var visited = new bool[height, width];
        var regionSizes = new List<int>();
        var totalTiles = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!visited[y, x] && !string.IsNullOrEmpty(tileMap[y, x]))
                {
                    var regionSize = FloodFill(tileMap, visited, x, y, tileMap[y, x]);
                    regionSizes.Add(regionSize);
                    totalTiles += regionSize;
                }
            }
        }

        if (regionSizes.Count == 0)
            return new RegionMetrics(0, 0, 0, 0, 0, 0, 0);

        var minSize = int.MaxValue;
        var maxSize = 0;
        var tilesInLargeRegions = 0;

        foreach (var size in regionSizes)
        {
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

    /// <summary>
    /// Analyzes tile type distribution across the map.
    /// Returns percentage of each tile type and region counts.
    /// </summary>
    public static DistributionMetrics AnalyzeDistribution(string[,] tileMap)
    {
        if (tileMap == null)
            throw new ArgumentNullException(nameof(tileMap));

        var height = tileMap.GetLength(0);
        var width = tileMap.GetLength(1);

        if (height == 0 || width == 0)
            return new DistributionMetrics(0, 0, []);

        // Count tiles per type and track regions
        var tileCounts = new Dictionary<string, int>();
        var tileRegions = new Dictionary<string, int>();
        var visited = new bool[height, width];
        var totalTiles = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var tile = tileMap[y, x];
                if (string.IsNullOrEmpty(tile))
                    continue;

                totalTiles++;

                if (!tileCounts.TryAdd(tile, 1))
                    tileCounts[tile]++;

                // Count regions via flood fill
                if (!visited[y, x])
                {
                    FloodFill(tileMap, visited, x, y, tile);
                    if (!tileRegions.TryAdd(tile, 1))
                        tileRegions[tile]++;
                }
            }
        }

        if (totalTiles == 0)
            return new DistributionMetrics(0, 0, []);

        var distributions = tileCounts
            .Select(kvp => new TileTypeDistribution(
                kvp.Key,
                kvp.Value,
                kvp.Value * 100f / totalTiles,
                tileRegions.GetValueOrDefault(kvp.Key, 0)))
            .OrderByDescending(d => d.Percentage)
            .ToArray();

        return new DistributionMetrics(totalTiles, tileCounts.Count, distributions);
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

/// <summary>
/// Statistics about contiguous regions in a tile map.
/// </summary>
public record RegionMetrics(
    int RegionCount,
    int MinSize,
    int MaxSize,
    float AverageSize,
    float PercentInLargeRegions,
    int TilesInLargeRegions,
    int TotalTiles
);

/// <summary>
/// Distribution statistics for a specific tile type.
/// </summary>
public record TileTypeDistribution(
    string TileId,
    int TileCount,
    float Percentage,
    int RegionCount
);

/// <summary>
/// Complete distribution analysis of all tile types in a map.
/// </summary>
public record DistributionMetrics(
    int TotalTiles,
    int UniqueTileTypes,
    TileTypeDistribution[] Distributions
)
{
    /// <summary>
    /// Returns true if all tile types are within the target percentage range.
    /// </summary>
    public bool IsWithinRange(float minPercent, float maxPercent)
    {
        foreach (var dist in Distributions)
        {
            if (dist.Percentage < minPercent || dist.Percentage > maxPercent)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Returns the maximum percentage of any single tile type.
    /// </summary>
    public float MaxPercentage => Distributions.Length > 0
        ? Distributions.Max(d => d.Percentage)
        : 0f;

    /// <summary>
    /// Returns the minimum percentage of any tile type.
    /// </summary>
    public float MinPercentage => Distributions.Length > 0
        ? Distributions.Min(d => d.Percentage)
        : 0f;
}
