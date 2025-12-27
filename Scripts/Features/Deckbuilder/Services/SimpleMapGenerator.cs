using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.BlobGeneration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Creates a simple connected map of passable/blocked tiles using biome-based tile selection
/// </summary>
public class SimpleMapGenerator
{
    public const string FloorTileId = "floor";
    public const string WallTileId = "wall";
    public const string GrassTileId = "grass";
    public const string DirtTileId = "dirt";
    public const string StoneTileId = "stone";
    public const string WaterTileId = "water";
    private readonly IBiomeProvider _biomeProvider;
    private readonly ITileRegistry _tileRegistry;
    private readonly RandomNumberGenerator _rng;
    private readonly TerrainBlobGenerator? _blobGenerator;

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        TerrainBlobGenerator? blobGenerator = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _blobGenerator = blobGenerator;
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating biome-based map {size.X}x{size.Y}");

        var tileIds = new string?[size.Y, size.X];
        var biomeMap = new BiomeType[size.Y, size.X];
        var passableTiles = new List<Vector2I>();
        var occupiedCells = new HashSet<Vector2I>();
        var multiTileSecondaryCells = new HashSet<Vector2I>(); // Secondary cells covered by multi-tiles

        // First pass: place tiles based on biome, respecting multi-tile sizes
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var biome = _biomeProvider.GetBiomeAt(position);
            biomeMap[y, x] = biome.Type;

            // Skip if already occupied by a multi-tile
            if (occupiedCells.Contains(position))
                continue;

            var isBlocked = _rng.Randf() < biome.BlockedPercentage;

            if (isBlocked)
            {
                // Try to place a blocked tile, retrying if multi-tile doesn't fit
                var placed = TryPlaceBlockedTile(tileIds, position, size, biome, occupiedCells, multiTileSecondaryCells);
                if (!placed)
                {
                    // Fallback: place as passable terrain if no blocked tile fits
                    tileIds[y, x] = SelectPassableTile(position, biome);
                    occupiedCells.Add(position);
                    passableTiles.Add(position);
                }
            }
            else
            {
                tileIds[y, x] = SelectPassableTile(position, biome);
                occupiedCells.Add(position);
                passableTiles.Add(position);
            }
        }

        // Second pass: fill any remaining empty cells with default terrain
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            if (tileIds[y, x] != null) continue;

            var position = new Vector2I(x, y);
            var biome = _biomeProvider.GetBiomeAt(position);
            tileIds[y, x] = SelectPassableTile(position, biome);

            // Only add to passable tiles if not covered by a multi-tile
            if (!multiTileSecondaryCells.Contains(position))
                passableTiles.Add(position);
        }

        // Ensure we have at least some passable tiles
        if (passableTiles.Count == 0)
        {
            var center = new Vector2I(size.X / 2, size.Y / 2);
            tileIds[center.Y, center.X] = FloorTileId;
            passableTiles.Add(center);
        }

        // Convert to non-nullable array (all cells should now be filled)
        var finalTileIds = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            finalTileIds[y, x] = tileIds[y, x] ?? FloorTileId;

        // Ensure all passable tiles are connected
        EnsureConnectivity(finalTileIds, size, passableTiles);

        // Choose random positions for player and enemies
        var shuffledTiles = passableTiles.OrderBy(_ => _rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];

        // Place 2-3 enemies randomly
        var enemyCount = _rng.RandiRange(2, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print(
            $"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, {enemyCount} enemies");

        return new SimpleMapData
        {
            TileIds = finalTileIds,
            BiomeMap = biomeMap,
            Size = size,
            PlayerStart = playerStart,
            EnemyPositions = enemyPositions,
            PassableTiles = passableTiles
        };
    }

    /// <summary>
    /// Try to place a blocked tile from the biome's blocked pool.
    /// If a multi-tile is selected but doesn't fit, retry with other tiles.
    /// </summary>
    private bool TryPlaceBlockedTile(string?[,] tileIds, Vector2I position, Vector2I mapSize,
        BiomeDefinition biome, HashSet<Vector2I> occupiedCells, HashSet<Vector2I> multiTileSecondaryCells)
    {
        const int maxAttempts = 5;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var tileId = biome.SelectBlockedTile(_rng);
            if (tileId == null) return false;

            var tile = _tileRegistry.GetTile(tileId);
            var tileSize = tile?.Size ?? Vector2I.One;

            // Single-cell tile always fits
            if (tileSize.X == 1 && tileSize.Y == 1)
            {
                tileIds[position.Y, position.X] = tileId;
                occupiedCells.Add(position);
                return true;
            }

            // Multi-tile - check if it fits
            if (CanPlaceMultiTile(position, tileSize, mapSize, occupiedCells))
            {
                PlaceMultiTile(tileIds, position, tileId, tileSize, occupiedCells, multiTileSecondaryCells);
                return true;
            }

            // Multi-tile doesn't fit, try again
        }

        return false; // No suitable tile found after max attempts
    }

    private static bool CanPlaceMultiTile(Vector2I position, Vector2I tileSize, Vector2I mapSize, HashSet<Vector2I> occupiedCells)
    {
        // Check if tile fits within map bounds
        if (position.X + tileSize.X > mapSize.X || position.Y + tileSize.Y > mapSize.Y)
            return false;

        // Check if all cells are unoccupied
        for (var dy = 0; dy < tileSize.Y; dy++)
        for (var dx = 0; dx < tileSize.X; dx++)
        {
            if (occupiedCells.Contains(new Vector2I(position.X + dx, position.Y + dy)))
                return false;
        }

        return true;
    }

    private static void PlaceMultiTile(string?[,] tileIds, Vector2I position, string tileId, Vector2I tileSize,
        HashSet<Vector2I> occupiedCells, HashSet<Vector2I> multiTileSecondaryCells)
    {
        // Place the tile ID at the anchor position (top-left)
        tileIds[position.Y, position.X] = tileId;
        occupiedCells.Add(position);

        // Mark remaining cells as occupied (secondary cells of multi-tile)
        for (var dy = 0; dy < tileSize.Y; dy++)
        for (var dx = 0; dx < tileSize.X; dx++)
        {
            if (dx == 0 && dy == 0) continue; // Skip anchor
            var cell = new Vector2I(position.X + dx, position.Y + dy);
            tileIds[cell.Y, cell.X] = null; // Will be filled in second pass
            occupiedCells.Add(cell);
            multiTileSecondaryCells.Add(cell); // Track as secondary (blocked for pathfinding)
        }
    }

    private void EnsureConnectivity(string[,] tileIds, Vector2I size, List<Vector2I> passableTiles)
    {
        if (passableTiles.Count <= 1) return;

        var visited = new bool[size.Y, size.X];
        var components = new List<List<Vector2I>>();

        foreach (var tile in passableTiles)
        {
            if (visited[tile.Y, tile.X]) continue;

            var component = new List<Vector2I>();
            FloodFill(tileIds, visited, size, tile, component);
            if (component.Count > 0) components.Add(component);
        }

        while (components.Count > 1)
        {
            var component1 = components[0];
            var component2 = components[1];

            var closest1 = component1[0];
            var closest2 = component2[0];
            var minDistance = float.MaxValue;

            foreach (var tile1 in component1)
            foreach (var tile2 in component2)
            {
                var distance = tile1.DistanceTo(tile2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest1 = tile1;
                    closest2 = tile2;
                }
            }

            CreateCorridor(tileIds, size, closest1, closest2);

            component1.AddRange(component2);
            components.RemoveAt(1);

            passableTiles.Clear();
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
                if (IsPassableTile(tileIds[y, x]))
                    passableTiles.Add(new Vector2I(x, y));
        }

        ILog.Print($"Connectivity ensured: {components.Count} connected component(s)");
    }

    private void FloodFill(string[,] tileIds, bool[,] visited, Vector2I size, Vector2I start,
        List<Vector2I> component)
    {
        var stack = new Stack<Vector2I>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.X < 0 || current.X >= size.X ||
                current.Y < 0 || current.Y >= size.Y ||
                visited[current.Y, current.X] ||
                !IsPassableTile(tileIds[current.Y, current.X]))
                continue;

            visited[current.Y, current.X] = true;
            component.Add(current);

            stack.Push(new Vector2I(current.X + 1, current.Y));
            stack.Push(new Vector2I(current.X - 1, current.Y));
            stack.Push(new Vector2I(current.X, current.Y + 1));
            stack.Push(new Vector2I(current.X, current.Y - 1));
        }
    }

    private void CreateCorridor(string[,] tileIds, Vector2I size, Vector2I from, Vector2I to)
    {
        var current = from;

        while (current.X != to.X)
        {
            current.X += current.X < to.X ? 1 : -1;
            if (IsValidPosition(current, size))
            {
                var biome = _biomeProvider.GetBiomeAt(current);
                tileIds[current.Y, current.X] = SelectPassableTile(current, biome);
            }
        }

        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            if (IsValidPosition(current, size))
            {
                var biome = _biomeProvider.GetBiomeAt(current);
                tileIds[current.Y, current.X] = SelectPassableTile(current, biome);
            }
        }
    }

    private static bool IsValidPosition(Vector2I pos, Vector2I size)
    {
        return pos.X >= 0 && pos.X < size.X && pos.Y >= 0 && pos.Y < size.Y;
    }

    private bool IsPassableTile(string tileId)
    {
        var tile = _tileRegistry.GetTile(tileId);
        return tile?.IsPassable ?? false;
    }

    /// <summary>
    /// Select a passable tile using blob generation when available, otherwise random selection.
    /// </summary>
    private string SelectPassableTile(Vector2I position, BiomeDefinition biome)
    {
        if (_blobGenerator != null)
        {
            var tile = _blobGenerator.SelectTileWithClustering(position, biome.PassableTiles, _rng);
            if (tile != null)
                return tile;
        }

        return biome.SelectPassableTile(_rng) ?? FloorTileId;
    }
}
