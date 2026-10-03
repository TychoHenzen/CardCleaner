using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Core map invariants that must hold for every generated map regardless of input signature.
/// </summary>
public static class MapInvariants
{
    private const int MaxCornerBitmask = 15;

    public static bool Validate(SimpleMapData map, int expectedWidth, int expectedHeight)
    {
        return HasDimensions(map, expectedWidth, expectedHeight)
               && map.PassableTiles.Count > 0
               && PlayerAndEnemiesArePlaced(map)
               && AllTileIdsNonNull(map, expectedWidth, expectedHeight)
               && PassableTilesWithinBounds(map, expectedWidth, expectedHeight)
               && DecorationOverlaysWithinVisualGrid(map, expectedWidth, expectedHeight)
               && DecorationBitmasksValid(map)
               && AllPassableTilesConnected(map);
    }

    public static bool HasDimensions(SimpleMapData map, int width, int height)
    {
        return map.Size.X == width && map.Size.Y == height
                                   && map.TileIds.GetLength(0) == height && map.TileIds.GetLength(1) == width
                                   && map.BiomeMap.GetLength(0) == height && map.BiomeMap.GetLength(1) == width;
    }

    public static bool PlayerAndEnemiesArePlaced(SimpleMapData map)
    {
        return map.PassableTiles.Contains(map.PlayerStart)
               && map.EnemyPositions.All(pos => map.PassableTiles.Contains(pos))
               && !map.EnemyPositions.Contains(map.PlayerStart);
    }

    public static bool AllTileIdsNonNull(SimpleMapData map, int width, int height)
    {
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            if (map.TileIds[y, x] == null)
                return false;

        return true;
    }

    public static bool PassableTilesWithinBounds(SimpleMapData map, int width, int height)
    {
        return map.PassableTiles.All(pos => pos.X >= 0 && pos.X < width && pos.Y >= 0 && pos.Y < height);
    }

    /// <summary>
    ///     Decoration overlays live on the visual grid, which is size+1 in each dimension.
    /// </summary>
    public static bool DecorationOverlaysWithinVisualGrid(SimpleMapData map, int width, int height)
    {
        return map.DecorationOverlays.Keys.All(position =>
            position.X >= 0 && position.X < width + 1 && position.Y >= 0 && position.Y < height + 1);
    }

    /// <summary>
    ///     All decoration overlays must carry valid 4-bit bitmasks (0-15).
    /// </summary>
    public static bool DecorationBitmasksValid(SimpleMapData map)
    {
        return map.DecorationOverlays.Values.All(overlay =>
            overlay.Bitmask >= 0 && overlay.Bitmask <= MaxCornerBitmask);
    }

    public static bool AllPassableTilesConnected(SimpleMapData map)
    {
        if (map.PassableTiles.Count <= 1)
            return true;

        var visited = new HashSet<Vector2I> { map.PassableTiles[0] };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(map.PassableTiles[0]);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in CardinalNeighbors(current))
            {
                if (map.PassableTiles.Contains(neighbor) && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        // All passable tiles should be visited
        return visited.Count == map.PassableTiles.Count;
    }

    private static Vector2I[] CardinalNeighbors(Vector2I tile)
    {
        return [tile + Vector2I.Up, tile + Vector2I.Right, tile + Vector2I.Down, tile + Vector2I.Left];
    }
}
