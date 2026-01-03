using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Structures;
using CardCleaner.Scripts.Features.Worldgen.VariantModifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Creates a connected map using WFC for terrain generation with biome-based tile selection.
/// Uses Wave Function Collapse for hard constraint satisfaction (2x2 window, adjacency rules)
/// then applies post-processing for structures, variants, and connectivity.
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
    /// Enable or disable structure placement during map generation.
    /// </summary>
    public bool StructuresEnabled { get; set; } = true;

    /// <summary>
    /// Maximum number of structures to place per map.
    /// </summary>
    public int MaxStructures { get; set; } = 5;

    /// <summary>
    /// Maximum WFC retry attempts on contradiction (default 5).
    /// </summary>
    public int MaxWfcRetries { get; set; } = 5;

    /// <summary>
    /// Enable or disable corridor fallback for connectivity.
    /// When true (default), EnsureConnectivity creates corridors between disconnected regions.
    /// When false, relies solely on WFC-native connectivity constraint.
    /// </summary>
    public bool EnableCorridorFallback { get; set; } = true;

    private readonly IBiomeProvider _biomeProvider;
    private readonly RandomNumberGenerator _rng;
    private readonly ITileRegistry _tileRegistry;
    private readonly WeightedVariantSelector? _variantSelector;
    private readonly StructurePlacer? _structurePlacer;
    private readonly WfcMapGenerator? _wfcGenerator;
    private readonly BiomeRegistry? _biomeRegistry;
    private readonly BaselineGradient? _gradient;
    private readonly List<StructureStamp> _structureStamps = [];

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        WfcMapGenerator? wfcGenerator = null, StructurePlacer? structurePlacer = null,
        WeightedVariantSelector? variantSelector = null, BiomeRegistry? biomeRegistry = null,
        BaselineGradient? gradient = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _wfcGenerator = wfcGenerator;
        _variantSelector = variantSelector;
        _structurePlacer = structurePlacer;
        _biomeRegistry = biomeRegistry;
        _gradient = gradient;
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
        ILog.Print($"Generating WFC-based map {size.X}x{size.Y}");

        // Pre-select per-generation variants
        var perGenerationVariants = SelectPerGenerationVariants();

        // Build biome map
        var biomeMap = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            biomeMap[y, x] = _biomeProvider.GetBiomeAt(new Vector2I(x, y)).Id;
        }

        // Generate terrain using WFC (with 2x2 window constraint built-in)
        var terrainGrid = GenerateTerrainViaWfc(size, biomeMap);

        // Build passable tiles list from WFC output
        var passableTiles = new List<Vector2I>();
        var placedTiles = new Dictionary<Vector2I, string>();
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var pos = new Vector2I(x, y);
            var tileId = terrainGrid[y, x];
            placedTiles[pos] = tileId;
            if (IsPassableTile(tileId))
                passableTiles.Add(pos);
        }

        // Ensure we have at least some passable tiles
        if (passableTiles.Count == 0)
        {
            var center = new Vector2I(size.X / 2, size.Y / 2);
            terrainGrid[center.Y, center.X] = FloorTileId;
            passableTiles.Add(center);
        }

        // Generate terrain transitions BEFORE placing structures
        var decorationOverlays = GenerateTerrainTransitions(terrainGrid, size);
        var transitionCount = decorationOverlays.Count(kvp => kvp.Value.Bitmask > 0 && kvp.Value.Bitmask < 15);
        ILog.Print($"Dual-grid terrain: {decorationOverlays.Count} visual tiles, {transitionCount} transitions");

        // Copy terrain to final grid for structure placement
        var finalTileIds = new string[size.Y, size.X];
        Array.Copy(terrainGrid, finalTileIds, terrainGrid.Length);

        // Place structures (on TOP of terrain, don't affect transitions)
        var structurePlacements = new List<StructurePlacement>();
        if (StructuresEnabled && _structurePlacer != null && _structureStamps.Count > 0)
        {
            _structurePlacer.Clear();
            structurePlacements = PlaceStructures(finalTileIds, size, passableTiles);
        }

        // Ensure all passable tiles are connected via corridors (if fallback enabled)
        if (EnableCorridorFallback)
        {
            EnsureConnectivity(finalTileIds, size, passableTiles);
        }

        // Select contextual variants
        var contextualVariants = SelectContextualVariants(finalTileIds, size, placedTiles, biomeMap);

        // Choose random positions for player and enemies
        var shuffledTiles = passableTiles.OrderBy(_ => _rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];

        // Place 2-3 enemies randomly
        var enemyCount = _rng.RandiRange(2, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print($"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, {enemyCount} enemies");

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
    /// Generates terrain grid using WFC with hard constraints (adjacency + 2x2 window).
    /// Falls back to simple random selection if WFC is not configured.
    /// </summary>
    private string[,] GenerateTerrainViaWfc(Vector2I size, string[,] biomeMap)
    {
        var terrainGrid = new string[size.Y, size.X];

        if (_wfcGenerator != null && _biomeRegistry != null)
        {
            _wfcGenerator.MaxRetries = MaxWfcRetries;

            var result = _wfcGenerator.GenerateMultiBiome(
                _biomeRegistry,
                pos => _biomeProvider.GetBiomeAt(pos),
                size,
                _rng.Randi(),
                _gradient);

            if (result.Success && result.MapData != null)
            {
                ILog.Print($"WFC generation succeeded in {result.Iterations} iterations");
                return result.MapData.TileIds;
            }

            ILog.Print($"WFC generation failed: {result.ErrorMessage}, falling back to simple generation");
        }

        // Fallback: simple random selection from biome pools
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var biome = _biomeProvider.GetBiomeAt(new Vector2I(x, y));
            terrainGrid[y, x] = biome.SelectPassableTile(_rng) ?? FloorTileId;
        }

        return terrainGrid;
    }

    /// <summary>
    /// Place structures on the map using registered stamps.
    /// </summary>
    private List<StructurePlacement> PlaceStructures(string[,] tileIds, Vector2I size, List<Vector2I> passableTiles)
    {
        var placements = new List<StructurePlacement>();

        if (_structurePlacer == null || _structureStamps.Count == 0)
            return placements;

        var passableSet = new HashSet<Vector2I>(passableTiles);
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

            var stamp = SelectWeightedStamp(weightedStamps);
            if (stamp == null) break;

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
                var lastPlacement = _structurePlacer.PlacedStructures[^1];
                for (var dy = 0; dy < lastPlacement.Result.Size.Y; dy++)
                for (var dx = 0; dx < lastPlacement.Result.Size.X; dx++)
                {
                    var structurePos = lastPlacement.Position + new Vector2I(dx, dy);
                    passableSet.Remove(structurePos);
                    passableTiles.Remove(structurePos);
                }
                placements.Add(lastPlacement);
            }
        }

        if (placedCount > 0)
            ILog.Print($"Structures placed: {placedCount} of {MaxStructures} max");

        return placements;
    }

    private StructureStamp? SelectWeightedStamp(List<StructureStamp> stamps)
    {
        if (stamps.Count == 0) return null;

        var totalWeight = stamps.Sum(s => s.SpawnWeight);
        if (totalWeight <= 0) return stamps[0];

        var roll = _rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var stamp in stamps)
        {
            cumulative += stamp.SpawnWeight;
            if (roll <= cumulative) return stamp;
        }

        return stamps[^1];
    }

    /// <summary>
    /// Ensure all passable tiles are connected by creating corridors between disconnected components.
    /// </summary>
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

    private void FloodFill(string[,] tileIds, bool[,] visited, Vector2I size, Vector2I start, List<Vector2I> component)
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
                tileIds[current.Y, current.X] = biome.SelectPassableTile(_rng) ?? FloorTileId;
            }
        }

        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            if (IsValidPosition(current, size))
            {
                var biome = _biomeProvider.GetBiomeAt(current);
                tileIds[current.Y, current.X] = biome.SelectPassableTile(_rng) ?? FloorTileId;
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

    private Dictionary<string, int> SelectPerGenerationVariants()
    {
        var variants = new Dictionary<string, int>();

        foreach (var tile in _tileRegistry.GetAllTiles())
        {
            if (tile.VariationMode == VariationMode.PerGeneration && tile.HasVariations)
            {
                var variantIndex = _rng.RandiRange(0, tile.Variations!.Length - 1);
                variants[tile.Id] = variantIndex;
            }
        }

        if (variants.Count > 0)
            ILog.Print($"Selected per-generation variants for {variants.Count} tile types");

        return variants;
    }

    private Dictionary<Vector2I, int> SelectContextualVariants(
        string[,] tileIds, Vector2I size, Dictionary<Vector2I, string> placedTiles, string[,] biomeMap)
    {
        var variants = new Dictionary<Vector2I, int>();

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

            if (tile.VariationMode != VariationMode.Contextual || !tile.HasVariations)
                continue;

            var biome = _biomeProvider.GetBiomeAt(position);
            var variantIndex = _variantSelector.SelectVariant(position, placedTiles, biome, tile, _rng);
            if (variantIndex >= 0)
                variants[position] = variantIndex;
        }

        if (variants.Count > 0)
            ILog.Print($"Selected contextual variants for {variants.Count} tile positions");

        return variants;
    }

    /// <summary>
    /// Generate dual-grid terrain transition data.
    /// Visual grid is (size+1) x (size+1), offset by half a tile from data grid.
    /// </summary>
    private Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> GenerateTerrainTransitions(
        string[,] terrainGrid, Vector2I size)
    {
        var overlays = new Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>();

        var visualWidth = size.X + 1;
        var visualHeight = size.Y + 1;

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var terrainTypes = new Dictionary<string, int>();
            var terrainInfo = new Dictionary<string, (int Dominance, bool HasAutoTile)>();

            SampleTerrainCell(terrainGrid, size, vx - 1, vy - 1, terrainTypes, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx, vy - 1, terrainTypes, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx - 1, vy, terrainTypes, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx, vy, terrainTypes, terrainInfo);

            string? baseTerrain = null;
            string? topTerrain = null;
            var baseHasAutoTile = false;
            var topHasAutoTile = false;
            var baseDominance = int.MaxValue;
            var topDominance = -1;

            foreach (var (terrain, (dominance, hasAutoTile)) in terrainInfo)
            {
                // Only consider terrains WITH auto-tiles for topTerrain
                if (hasAutoTile)
                {
                    if (topTerrain == null ||
                        dominance > topDominance ||
                        (dominance == topDominance && string.CompareOrdinal(terrain, topTerrain) < 0))
                    {
                        topTerrain = terrain;
                        topDominance = dominance;
                        topHasAutoTile = true;
                    }
                }

                // baseTerrain prefers tiles WITHOUT auto-tiles (fill terrains)
                if (!hasAutoTile)
                {
                    if (baseTerrain == null ||
                        dominance < baseDominance ||
                        (dominance == baseDominance && string.CompareOrdinal(terrain, baseTerrain) < 0))
                    {
                        baseTerrain = terrain;
                        baseDominance = dominance;
                    }
                }
            }

// If no auto-tile terrain found, use the base (fill) terrain for both
// This renders as solid fill with bitmask 15
            if (topTerrain == null)
            {
                topTerrain = baseTerrain ?? FloorTileId;
            }
            baseTerrain ??= topTerrain;


            if (topTerrain == null)
            {
                topTerrain = FloorTileId;
                baseTerrain = FloorTileId;
            }
            else
            {
                baseTerrain ??= topTerrain;
            }

            var bitmask = 0;
            if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy - 1, topTerrain))
                bitmask |= NeighborBitmaskCorner.NorthWest;
            if (IsTerrainAtPosition(terrainGrid, size, vx, vy - 1, topTerrain))
                bitmask |= NeighborBitmaskCorner.NorthEast;
            if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy, topTerrain))
                bitmask |= NeighborBitmaskCorner.SouthWest;
            if (IsTerrainAtPosition(terrainGrid, size, vx, vy, topTerrain))
                bitmask |= NeighborBitmaskCorner.SouthEast;

            var visualPosition = new Vector2I(vx, vy);
            overlays[visualPosition] = (baseTerrain, topTerrain, bitmask);
        }

        return overlays;
    }

    private void SampleTerrainCell(string[,] terrainGrid, Vector2I size, int x, int y,
        Dictionary<string, int> terrainTypes, Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);

        var tileId = terrainGrid[y, x];
        var tile = _tileRegistry.GetTile(tileId);

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

    private static bool IsTerrainAtPosition(string[,] terrainGrid, Vector2I size, int x, int y, string terrainTileId)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);
        return terrainGrid[y, x] == terrainTileId;
    }
}
