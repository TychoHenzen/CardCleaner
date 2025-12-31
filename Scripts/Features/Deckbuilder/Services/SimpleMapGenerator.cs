using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Structures;
using CardCleaner.Scripts.Features.Worldgen.VariantModifiers;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.WeightModifiers;
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

    /// <summary>
    /// Enable or disable structure placement during map generation.
    /// </summary>
    public bool StructuresEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of structures to place per map.
    /// </summary>
    public int MaxStructures { get; set; } = 5;

    private readonly IBiomeProvider _biomeProvider;
    private readonly RandomNumberGenerator _rng;
    private readonly ITileRegistry _tileRegistry;
    private readonly WeightedTileSelector? _weightedSelector;
    private readonly WeightedVariantSelector? _variantSelector;
    private readonly StructurePlacer? _structurePlacer;
    private readonly List<StructureStamp> _structureStamps = [];

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        WeightedTileSelector? weightedSelector = null, StructurePlacer? structurePlacer = null,
        WeightedVariantSelector? variantSelector = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _weightedSelector = weightedSelector;
        _variantSelector = variantSelector;
        _structurePlacer = structurePlacer;
    }

    /// <summary>
    /// Add a structure stamp to be placed during map generation.
    /// </summary>
    public void AddStructureStamp(StructureStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        _structureStamps.Add(stamp);
    }

    /// <summary>
    /// Clear all registered structure stamps.
    /// </summary>
    public void ClearStructureStamps() => _structureStamps.Clear();

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating biome-based map {size.X}x{size.Y}");

        // Pre-select per-generation variants for all tiles that use VariationMode.PerGeneration
        var perGenerationVariants = SelectPerGenerationVariants();

        var biomeMap = new BiomeType[size.Y, size.X];
        var placedTiles = new Dictionary<Vector2I, string>();

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
                var tileId = SelectPassableTile(position, biome, placedTiles);
                tileIds[y, x] = tileId;
                occupiedCells.Add(position);
                placedTiles[position] = tileId;
                passableTiles.Add(position);
            }
            else
            {
                var placed = TryPlaceBlockedTile(tileIds, position, size, biome, occupiedCells,
                    multiTileSecondaryCells, placedTiles);
                if (!placed)
                {
                    var tileId = SelectPassableTile(position, biome, placedTiles);
                    tileIds[y, x] = tileId;
                    occupiedCells.Add(position);
                    placedTiles[position] = tileId;
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
            var tileId = SelectPassableTile(position, biome, placedTiles);
            tileIds[y, x] = tileId;

            if (!multiTileSecondaryCells.Contains(position))
            {
                placedTiles[position] = tileId;
                passableTiles.Add(position);
            }
        }

        // Ensure we have at least some passable tiles
        if (passableTiles.Count == 0)
        {
            var center = new Vector2I(size.X / 2, size.Y / 2);
            tileIds[center.Y, center.X] = FloorTileId;
            passableTiles.Add(center);
        }

        // Convert to non-nullable array - this becomes our TERRAIN grid
        // We keep this separate from structure data for dual-grid calculations
        var terrainGrid = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            terrainGrid[y, x] = tileIds[y, x] ?? FloorTileId;

        // Generate terrain transitions BEFORE placing structures
        // This ensures the dual-grid system sees only terrain data
        var decorationOverlays = GenerateTerrainTransitions(terrainGrid, size);
        var transitionCount = decorationOverlays.Count(kvp => kvp.Value.Bitmask > 0 && kvp.Value.Bitmask < 15);
        ILog.Print($"Dual-grid terrain: {decorationOverlays.Count} visual tiles, {transitionCount} transitions");

        // Now copy terrain to final grid for structure placement
        var finalTileIds = new string[size.Y, size.X];
        Array.Copy(terrainGrid, finalTileIds, terrainGrid.Length);

        // Phase 3.5: Place structures (these go on TOP of terrain, don't affect transitions)
        var structurePlacements = new List<StructurePlacement>();
        if (StructuresEnabled && _structurePlacer != null && _structureStamps.Count > 0)
        {
            _structurePlacer.Clear();
            structurePlacements = PlaceStructures(finalTileIds, size, passableTiles);
        }

        // Ensure all passable tiles are connected
        EnsureConnectivity(finalTileIds, size, passableTiles);

        // Select contextual variants for tiles that use VariationMode.Contextual
        var contextualVariants = SelectContextualVariants(finalTileIds, size, placedTiles, biomeMap);

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
            ContextualVariants = contextualVariants,
            DecorationOverlays = decorationOverlays,
            StructurePlacements = structurePlacements
        };
    }

    /// <summary>
    /// Place structures on the map using registered stamps.
    /// </summary>
    private List<StructurePlacement> PlaceStructures(string[,] tileIds, Vector2I size, List<Vector2I> passableTiles)
    {
        var placements = new List<StructurePlacement>();

        if (_structurePlacer == null || _structureStamps.Count == 0)
            return placements;

        // Create a set for fast passability lookups
        var passableSet = new HashSet<Vector2I>(passableTiles);

        // Sort stamps by spawn weight for weighted selection
        var weightedStamps = _structureStamps
            .Where(s => s.SpawnWeight > 0)
            .OrderByDescending(s => s.SpawnWeight)
            .ToList();

        var placedCount = 0;
        var attempts = 0;
        const int maxAttempts = 100;

        while (placedCount < MaxStructures && attempts < maxAttempts && weightedStamps.Count > 0)
        {
            attempts++;

            // Weighted random selection of stamp
            var stamp = SelectWeightedStamp(weightedStamps);
            if (stamp == null)
                break;

            // Try to place the stamp
            var placed = _structurePlacer.TryPlaceRandom(
                stamp,
                size,
                pos => _biomeProvider.GetBiomeAt(pos),
                pos => passableSet.Contains(pos),
                tileIds,
                _rng);

            if (placed)
            {
                placedCount++;

                // Update passable tiles - remove tiles covered by structure
                var lastPlacement = _structurePlacer.PlacedStructures[^1];
                for (var dy = 0; dy < lastPlacement.Result.Size.Y; dy++)
                {
                    for (var dx = 0; dx < lastPlacement.Result.Size.X; dx++)
                    {
                        var structurePos = lastPlacement.Position + new Vector2I(dx, dy);
                        passableSet.Remove(structurePos);
                        passableTiles.Remove(structurePos);
                    }
                }

                placements.Add(lastPlacement);
            }
        }

        if (placedCount > 0)
            ILog.Print($"Structures placed: {placedCount} of {MaxStructures} max");

        return placements;
    }

    /// <summary>
    /// Select a stamp using weighted random selection.
    /// </summary>
    private StructureStamp? SelectWeightedStamp(List<StructureStamp> stamps)
    {
        if (stamps.Count == 0)
            return null;

        var totalWeight = stamps.Sum(s => s.SpawnWeight);
        if (totalWeight <= 0)
            return stamps[0];

        var roll = _rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var stamp in stamps)
        {
            cumulative += stamp.SpawnWeight;
            if (roll <= cumulative)
                return stamp;
        }

        return stamps[^1];
    }

    /// <summary>
    /// Try to place a blocked tile from the biome's blocked pool.
    /// If a multi-tile is selected but doesn't fit, retry with other tiles.
    /// </summary>
    private bool TryPlaceBlockedTile(string?[,] tileIds, Vector2I position, Vector2I mapSize,
        BiomeDefinition biome, HashSet<Vector2I> occupiedCells, HashSet<Vector2I> multiTileSecondaryCells,
        Dictionary<Vector2I, string> placedTiles)
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
                placedTiles[position] = tileId;
                return true;
            }

            // Multi-tile - check if it fits
            if (CanPlaceMultiTile(position, tileSize, mapSize, occupiedCells))
            {
                PlaceMultiTile(tileIds, position, tileId, tileSize, occupiedCells, multiTileSecondaryCells);
                placedTiles[position] = tileId;
                // Track secondary cells as well
                for (var dy = 0; dy < tileSize.Y; dy++)
                for (var dx = 0; dx < tileSize.X; dx++)
                    if (!(dx == 0 && dy == 0))
                        placedTiles[position + new Vector2I(dx, dy)] = tileId;
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
        // Build placedTiles from current array state for corridor creation
        var placedTiles = BuildPlacedTilesFromArray(tileIds, size);
        var current = from;

        while (current.X != to.X)
        {
            current.X += current.X < to.X ? 1 : -1;
            if (IsValidPosition(current, size))
            {
                var biome = _biomeProvider.GetBiomeAt(current);
                var tileId = SelectPassableTile(current, biome, placedTiles);
                tileIds[current.Y, current.X] = tileId;
                placedTiles[current] = tileId;
            }
        }

        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            if (IsValidPosition(current, size))
            {
                var biome = _biomeProvider.GetBiomeAt(current);
                var tileId = SelectPassableTile(current, biome, placedTiles);
                tileIds[current.Y, current.X] = tileId;
                placedTiles[current] = tileId;
            }
        }
    }

    private static Dictionary<Vector2I, string> BuildPlacedTilesFromArray(string[,] tileIds, Vector2I size)
    {
        var result = new Dictionary<Vector2I, string>();
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var tile = tileIds[y, x];
            if (tile != null)
                result[new Vector2I(x, y)] = tile;
        }
        return result;
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
    /// Select a passable tile using weighted selection when available,
    /// falling back to blob generation or random selection.
    /// </summary>
    private string SelectPassableTile(Vector2I position, BiomeDefinition biome,
        IReadOnlyDictionary<Vector2I, string> placedTiles)
    {
        if (_weightedSelector != null)
        {
            var candidateTiles = biome.PassableTiles.GetAllTileIds();
            var selectedTile = _weightedSelector.SelectTile(position, placedTiles, biome, _rng, candidateTiles);
            if (selectedTile != null)
                return selectedTile;
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
    /// Select contextual variants for tiles that use VariationMode.Contextual.
    /// Variants are selected based on position, biome, and nearby tiles.
    /// </summary>
    private Dictionary<Vector2I, int> SelectContextualVariants(
        string[,] tileIds,
        Vector2I size,
        Dictionary<Vector2I, string> placedTiles,
        BiomeType[,] biomeMap)
    {
        var variants = new Dictionary<Vector2I, int>();

        // If no variant selector is configured, return empty dictionary
        if (_variantSelector == null)
            return variants;

        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var position = new Vector2I(x, y);
            var tileId = tileIds[y, x];

            if (string.IsNullOrEmpty(tileId))
                continue;

            var tile = _tileRegistry.GetTile(tileId);
            if (tile == null)
                continue;

            // Only process tiles with Contextual variation mode that have variants
            if (tile.VariationMode != VariationMode.Contextual || !tile.HasVariations)
                continue;

            // Get biome at position
            var biome = _biomeProvider.GetBiomeAt(position);

            // Select variant using the weighted variant selector
            var variantIndex = _variantSelector.SelectVariant(position, placedTiles, biome, tile, _rng);
            if (variantIndex >= 0)
            {
                variants[position] = variantIndex;
            }
        }

        if (variants.Count > 0)
            ILog.Print($"Selected contextual variants for {variants.Count} tile positions");

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
    /// Generate complete dual-grid terrain data with base and top layers.
    /// Visual grid is (size+1) x (size+1), offset by half a tile from data grid.
    /// Each visual position samples 4 corner data cells to determine terrain and bitmask.
    /// Base layer fills background, top layer provides auto-tiled transitions.
    /// </summary>
    private Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> GenerateTerrainTransitions(
        string[,] terrainGrid, Vector2I size)
    {
        var overlays = new Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>();

        // Visual grid is (size+1) x (size+1) since visual tiles sit at intersections
        // Visual tile at (vx, vy) samples terrain cells at corners:
        // TL = (vx-1, vy-1), TR = (vx, vy-1), BL = (vx-1, vy), BR = (vx, vy)
        var visualWidth = size.X + 1;
        var visualHeight = size.Y + 1;

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            // Sample the 4 corner terrain cells and track all terrain types
            var terrainTypes = new Dictionary<string, int>(); // tile id -> count
            var terrainInfo = new Dictionary<string, (int Dominance, bool HasAutoTile)>(); // tile id -> (dominance, hasAutoTile)

            // TL corner: terrain cell (vx-1, vy-1)
            SampleTerrainCell(terrainGrid, size, vx - 1, vy - 1, terrainTypes, terrainInfo);
            // TR corner: terrain cell (vx, vy-1)
            SampleTerrainCell(terrainGrid, size, vx, vy - 1, terrainTypes, terrainInfo);
            // BL corner: terrain cell (vx-1, vy)
            SampleTerrainCell(terrainGrid, size, vx - 1, vy, terrainTypes, terrainInfo);
            // BR corner: terrain cell (vx, vy)
            SampleTerrainCell(terrainGrid, size, vx, vy, terrainTypes, terrainInfo);

            // Determine top and base terrain for dual-grid auto-tiling
            // Top terrain = the auto-tile that provides the border/transition shape
            // Base terrain = the background terrain that gets overlaid
            //
            // Selection priority:
            // 1. Auto-tile terrains become "top" (they have the transition sprites)
            // 2. Non-auto-tile terrains become "base" (they're the background)
            // 3. If both have auto-tiles or neither does, use dominance as tiebreaker
            string? baseTerrain = null;
            string? topTerrain = null;
            var baseHasAutoTile = false;
            var topHasAutoTile = false;
            var baseDominance = int.MaxValue;
            var topDominance = -1;

            foreach (var (terrain, (dominance, hasAutoTile)) in terrainInfo)
            {
                // Determine if this terrain should become the new top
                var shouldBeTop = false;
                if (topTerrain == null)
                {
                    shouldBeTop = true;
                }
                else if (hasAutoTile && !topHasAutoTile)
                {
                    // Auto-tile beats non-auto-tile for top position
                    shouldBeTop = true;
                }
                else if (hasAutoTile == topHasAutoTile)
                {
                    // Same auto-tile status: use dominance, then alphabetical
                    if (dominance > topDominance ||
                        (dominance == topDominance && string.CompareOrdinal(terrain, topTerrain) < 0))
                    {
                        shouldBeTop = true;
                    }
                }

                // Determine if this terrain should become the new base
                var shouldBeBase = false;
                if (baseTerrain == null)
                {
                    shouldBeBase = true;
                }
                else if (!hasAutoTile && baseHasAutoTile)
                {
                    // Non-auto-tile beats auto-tile for base position
                    shouldBeBase = true;
                }
                else if (hasAutoTile == baseHasAutoTile)
                {
                    // Same auto-tile status: use dominance (lower wins for base), then alphabetical
                    if (dominance < baseDominance ||
                        (dominance == baseDominance && string.CompareOrdinal(terrain, baseTerrain) < 0))
                    {
                        shouldBeBase = true;
                    }
                }

                if (shouldBeTop)
                {
                    topTerrain = terrain;
                    topDominance = dominance;
                    topHasAutoTile = hasAutoTile;
                }
                if (shouldBeBase)
                {
                    baseTerrain = terrain;
                    baseDominance = dominance;
                    baseHasAutoTile = hasAutoTile;
                }
            }

            // Use fallback terrain if none found (edge of map with no valid corners)
            if (topTerrain == null)
            {
                topTerrain = FloorTileId;
                baseTerrain = FloorTileId;
            }
            else
            {
                baseTerrain ??= topTerrain;
            }

            // Compute Corner16 bitmask: which corners have the top terrain?
            // Always check each corner - can't assume all corners have same terrain
            // because some corners may have non-terrain tiles (decorations) that were skipped
            var bitmask = 0;
            if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy - 1, topTerrain))
                bitmask |= NeighborBitmaskCorner.NorthWest; // 8
            if (IsTerrainAtPosition(terrainGrid, size, vx, vy - 1, topTerrain))
                bitmask |= NeighborBitmaskCorner.NorthEast; // 1
            if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy, topTerrain))
                bitmask |= NeighborBitmaskCorner.SouthWest; // 4
            if (IsTerrainAtPosition(terrainGrid, size, vx, vy, topTerrain))
                bitmask |= NeighborBitmaskCorner.SouthEast; // 2

            // Store complete dual-grid data for ALL visual positions
            var visualPosition = new Vector2I(vx, vy);
            overlays[visualPosition] = (baseTerrain, topTerrain, bitmask);
        }

        return overlays;
    }

    /// <summary>
    /// Sample a terrain cell and add it to the terrain type tracking dictionaries.
    /// Only terrain-layer tiles participate in transitions; decorations etc. are skipped.
    /// Out-of-bounds coordinates are clamped to nearest valid cell for edge handling.
    /// </summary>
    private void SampleTerrainCell(string[,] terrainGrid, Vector2I size, int x, int y,
        Dictionary<string, int> terrainTypes, Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        // Clamp coordinates to valid range
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);

        var tileId = terrainGrid[y, x];
        var tile = _tileRegistry.GetTile(tileId);

        // Skip non-terrain tiles - only Layer.Terrain participates in auto-tiling
        if (tile == null || tile.Layer != TileLayer.Terrain)
            return;

        if (terrainTypes.TryGetValue(tileId, out var count))
            terrainTypes[tileId] = count + 1;
        else
        {
            terrainTypes[tileId] = 1;
            terrainInfo[tileId] = (tile.Dominance, tile.HasAutoTileVariants);
        }
    }

    /// <summary>
    /// Check if the terrain at the given position matches the specified terrain tile ID.
    /// Out-of-bounds coordinates are clamped to nearest valid cell for consistent edge handling.
    /// </summary>
    private static bool IsTerrainAtPosition(string[,] terrainGrid, Vector2I size, int x, int y, string terrainTileId)
    {
        // Clamp coordinates to valid range for consistent edge handling
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);

        return terrainGrid[y, x] == terrainTileId;
    }

}
