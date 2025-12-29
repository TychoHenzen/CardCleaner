using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
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

    /// <summary>
    /// Number of cellular automata smoothing iterations to apply.
    /// Higher values create larger contiguous regions but may reduce variety.
    /// </summary>
    public int SmoothingIterations { get; set; } = 3;

    /// <summary>
    /// Minimum neighbor count required for a cell to become passable during smoothing.
    /// Range 1-4 (4-directional neighbors). Lower values create more passable terrain.
    /// </summary>
    public int SmoothingThreshold { get; set; } = 2;

    private readonly AutoTileResolver? _autoTileResolver;
    private readonly IBiomeProvider _biomeProvider;
    private readonly TerrainBlobGenerator? _blobGenerator;
    private readonly RandomNumberGenerator _rng;
    private readonly ITileRegistry _tileRegistry;

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        TerrainBlobGenerator? blobGenerator = null, AutoTileResolver? autoTileResolver = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _blobGenerator = blobGenerator;
        _autoTileResolver = autoTileResolver;
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating biome-based map {size.X}x{size.Y}");

        // Pre-select per-generation variants for all tiles that use VariationMode.PerGeneration
        var perGenerationVariants = SelectPerGenerationVariants();

        var biomeMap = new BiomeType[size.Y, size.X];

        // Phase 1: Generate initial passability map based on biome BlockedPercentage
        var isPassable = new bool[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var biome = _biomeProvider.GetBiomeAt(position);
            biomeMap[y, x] = biome.Type;
            isPassable[y, x] = _rng.Randf() >= biome.BlockedPercentage;
        }

        // Phase 2: Apply cellular automata smoothing to create larger contiguous regions
        if (SmoothingIterations > 0)
        {
            isPassable = ApplyRegionSmoothing(isPassable, biomeMap, size);
            ILog.Print($"Applied {SmoothingIterations} smoothing iterations (threshold={SmoothingThreshold})");
        }

        // Phase 3: Place tiles based on smoothed passability map
        var tileIds = new string?[size.Y, size.X];
        var passableTiles = new List<Vector2I>();
        var occupiedCells = new HashSet<Vector2I>();
        var multiTileSecondaryCells = new HashSet<Vector2I>();

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var biome = _biomeProvider.GetBiomeAt(position);

            if (occupiedCells.Contains(position))
                continue;

            if (isPassable[y, x])
            {
                tileIds[y, x] = SelectPassableTile(position, biome);
                occupiedCells.Add(position);
                passableTiles.Add(position);
            }
            else
            {
                var placed = TryPlaceBlockedTile(tileIds, position, size, biome, occupiedCells,
                    multiTileSecondaryCells);
                if (!placed)
                {
                    tileIds[y, x] = SelectPassableTile(position, biome);
                    occupiedCells.Add(position);
                    passableTiles.Add(position);
                }
            }
        }

        // Fill any remaining empty cells (from multi-tile secondary cells)
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            if (tileIds[y, x] != null) continue;

            var position = new Vector2I(x, y);
            var biome = _biomeProvider.GetBiomeAt(position);
            tileIds[y, x] = SelectPassableTile(position, biome);

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

        // Convert to non-nullable array
        var finalTileIds = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            finalTileIds[y, x] = tileIds[y, x] ?? FloorTileId;

        // Ensure all passable tiles are connected
        EnsureConnectivity(finalTileIds, size, passableTiles);

        // Detect terrain transitions and generate decoration overlays
        var decorationOverlays = GenerateTerrainTransitions(finalTileIds, size);
        if (decorationOverlays.Count > 0)
            ILog.Print($"Terrain transitions: {decorationOverlays.Count} decoration overlays generated");

        // Apply auto-tiling post-processing (select edge variants based on neighbors)
        if (_autoTileResolver != null && _autoTileResolver.ConfigCount > 0)
        {
            var replacements = _autoTileResolver.ApplyToMap(finalTileIds, size);
            if (replacements > 0)
                ILog.Print($"Auto-tiling applied: {replacements} tiles replaced with edge variants");
        }

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
            PassableTiles = passableTiles,
            PerGenerationVariants = perGenerationVariants,
            DecorationOverlays = decorationOverlays
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

            // Check decoration density - if this decoration shouldn't appear, try another
            if (tile != null && !tile.ShouldPlaceDecoration(_rng))
                continue;

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

    private static bool CanPlaceMultiTile(Vector2I position, Vector2I tileSize, Vector2I mapSize,
        HashSet<Vector2I> occupiedCells)
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

    /// <summary>
    /// Pre-select variation indices for all tiles that use VariationMode.PerGeneration.
    /// This ensures consistent appearance across the entire map for themed tiles.
    /// </summary>
    private Dictionary<string, int> SelectPerGenerationVariants()
    {
        var variants = new Dictionary<string, int>();

        foreach (var tile in _tileRegistry.GetAllTiles())
        {
            if (tile.VariationMode == VariationMode.PerGeneration && tile.HasVariations)
            {
                // Select a random variation index for this tile type
                var variantIndex = _rng.RandiRange(0, tile.Variations!.Length - 1);
                variants[tile.Id] = variantIndex;
            }
        }

        if (variants.Count > 0)
            ILog.Print($"Selected per-generation variants for {variants.Count} tile types");

        return variants;
    }

    /// <summary>
    /// Apply cellular automata smoothing to create larger contiguous regions.
    /// Uses 4-directional neighbor counting with biome boundary preservation.
    /// </summary>
    private bool[,] ApplyRegionSmoothing(bool[,] isPassable, BiomeType[,] biomeMap, Vector2I size)
    {
        var current = isPassable;
        var next = new bool[size.Y, size.X];

        for (var iteration = 0; iteration < SmoothingIterations; iteration++)
        {
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var passableNeighbors = CountPassableNeighbors(current, biomeMap, size, x, y);

                // Apply threshold: cell becomes passable if enough neighbors are passable
                // This creates larger contiguous regions of both passable and blocked tiles
                next[y, x] = passableNeighbors >= SmoothingThreshold;
            }

            // Swap buffers for next iteration
            (current, next) = (next, current);
        }

        return current;
    }

    /// <summary>
    /// Count passable neighbors in 4 cardinal directions, respecting biome boundaries.
    /// Cells in different biomes are treated as blocked for smoothing purposes.
    /// </summary>
    private static int CountPassableNeighbors(bool[,] isPassable, BiomeType[,] biomeMap, Vector2I size, int x, int y)
    {
        var count = 0;
        var currentBiome = biomeMap[y, x];

        // Check 4 cardinal neighbors (N, E, S, W)
        ReadOnlySpan<(int dx, int dy)> neighbors = [(0, -1), (1, 0), (0, 1), (-1, 0)];

        foreach (var (dx, dy) in neighbors)
        {
            var nx = x + dx;
            var ny = y + dy;

            // Out of bounds counts as blocked
            if (nx < 0 || nx >= size.X || ny < 0 || ny >= size.Y)
                continue;

            // Different biome counts as blocked (preserves biome boundaries)
            if (biomeMap[ny, nx] != currentBiome)
                continue;

            if (isPassable[ny, nx])
                count++;
        }

        return count;
    }

    /// <summary>
    /// Generate decoration overlays for terrain transitions.
    /// When a dominant terrain type borders a less dominant one, the dominant terrain's
    /// edge graphics are placed on the decoration layer over the less dominant terrain.
    /// </summary>
    private Dictionary<Vector2I, (string TileId, int Bitmask)> GenerateTerrainTransitions(
        string[,] tileIds, Vector2I size)
    {
        var overlays = new Dictionary<Vector2I, (string TileId, int Bitmask)>();

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var currentTile = tileIds[y, x];
            var currentBase = GetBaseTileId(currentTile);
            var currentDominance = GetTerrainDominance(currentBase);

            // Check all 4 cardinal neighbors for more dominant terrain
            string? dominantNeighborBase = null;
            var dominantNeighborDominance = currentDominance;
            var bitmask = 0;

            // N=1, E=2, S=4, W=8 (4-bit cardinal bitmask)
            ReadOnlySpan<(int dx, int dy, int bit)> neighbors = [(0, -1, 1), (1, 0, 2), (0, 1, 4), (-1, 0, 8)];

            foreach (var (dx, dy, bit) in neighbors)
            {
                var nx = x + dx;
                var ny = y + dy;

                if (nx < 0 || nx >= size.X || ny < 0 || ny >= size.Y)
                    continue;

                var neighborTile = tileIds[ny, nx];
                var neighborBase = GetBaseTileId(neighborTile);

                // Skip if same base terrain type
                if (neighborBase == currentBase)
                    continue;

                var neighborDominance = GetTerrainDominance(neighborBase);

                // If this neighbor is more dominant than current, track it
                if (neighborDominance > dominantNeighborDominance)
                {
                    dominantNeighborBase = neighborBase;
                    dominantNeighborDominance = neighborDominance;
                    bitmask = bit;
                }
                else if (neighborDominance == dominantNeighborDominance && neighborBase == dominantNeighborBase)
                {
                    // Same dominant terrain in another direction - add to bitmask
                    bitmask |= bit;
                }
            }

            // If we found a more dominant neighbor, create decoration overlay
            if (dominantNeighborBase != null && bitmask > 0)
            {
                overlays[position] = (dominantNeighborBase, bitmask);
            }
        }

        return overlays;
    }

    /// <summary>
    /// Get the base tile ID by stripping numeric suffixes and edge variant suffixes.
    /// e.g., "grass_1" -> "grass", "sand_edge_3" -> "sand", "stone" -> "stone"
    /// </summary>
    private static string GetBaseTileId(string tileId)
    {
        // Remove common suffixes that indicate variants
        var result = tileId;

        // Strip "_edge_N" suffix first
        var edgeIndex = result.IndexOf("_edge", StringComparison.Ordinal);
        if (edgeIndex > 0)
            result = result[..edgeIndex];

        // Strip trailing "_N" numeric suffix
        var lastUnderscore = result.LastIndexOf('_');
        if (lastUnderscore > 0 && lastUnderscore < result.Length - 1)
        {
            var suffix = result[(lastUnderscore + 1)..];
            if (int.TryParse(suffix, out _))
                result = result[..lastUnderscore];
        }

        return result;
    }

    /// <summary>
    /// Get the visual dominance of a terrain type.
    /// Higher values mean the terrain's edges will render over lower-dominance terrain.
    /// This determines which terrain "wins" at boundaries.
    /// </summary>
    private static int GetTerrainDominance(string baseTileId)
    {
        // Dominance hierarchy: higher value = more dominant (edges show over less dominant)
        // Solid/dense terrain dominates over soft/passable terrain
        return baseTileId switch
        {
            "wall" or "stone" or "rock" => 100,           // Walls/stone most dominant
            "water" or "lava" => 90,                       // Liquids
            "mountains" or "cliff" => 80,                  // Mountain terrain
            "forest" or "trees" => 70,                     // Dense vegetation
            "swamp" or "marsh" => 60,                      // Wet terrain
            "sand" or "desert" => 50,                      // Desert terrain
            "dirt" or "mud" => 40,                         // Dirt variants
            "snow" or "ice" or "tundra" => 30,             // Cold terrain
            "grass" or "plains" => 20,                     // Basic grass
            "floor" => 10,                                 // Interior floors
            _ => 25                                        // Default middle value
        };
    }
}
