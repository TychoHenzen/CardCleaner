using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
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
    /// Maximum WFC retry attempts on contradiction (default 5).
    /// </summary>
    public int MaxWfcRetries { get; set; } = 5;

    private readonly IBiomeProvider _biomeProvider;
    private readonly RandomNumberGenerator _rng;
    private readonly ITileRegistry _tileRegistry;
    private readonly WfcMapGenerator? _wfcGenerator;
    private readonly BiomeRegistry? _biomeRegistry;
    private readonly BaselineGradient? _gradient;
    private IProfiler _profiler = new NoOpProfiler();

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        WfcMapGenerator? wfcGenerator = null, BiomeRegistry? biomeRegistry = null,
        BaselineGradient? gradient = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _wfcGenerator = wfcGenerator;
        _biomeRegistry = biomeRegistry;
        _gradient = gradient;
    }

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
        _wfcGenerator?.SetProfiler(profiler);
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating WFC-based map {size.X}x{size.Y}");

        // Pre-select per-generation variants
        Dictionary<string, int> perGenerationVariants;
        using (_profiler.BeginScope("VariantSelection"))
        {
            perGenerationVariants = SelectPerGenerationVariants();
        }

        // Build biome map
        string[,] biomeMap;
        using (_profiler.BeginScope("BiomeMapBuild"))
        {
            biomeMap = new string[size.Y, size.X];
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                biomeMap[y, x] = _biomeProvider.GetBiomeAt(new Vector2I(x, y)).Id;
            }
        }

        // Generate terrain using WFC (with 2x2 window constraint built-in)
        string[,] terrainGrid;
        using (_profiler.BeginScope("WfcTerrainGeneration"))
        {
            terrainGrid = GenerateTerrainViaWfc(size, biomeMap);
        }

        // Build passable tiles list from WFC output
        List<Vector2I> passableTiles;
        using (_profiler.BeginScope("PassableTileCollection"))
        {
            passableTiles = new List<Vector2I>();
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
        }

        // Generate terrain transitions BEFORE placing structures
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> decorationOverlays;
        using (_profiler.BeginScope("TerrainTransitions"))
        {
            decorationOverlays = GenerateTerrainTransitions(terrainGrid, size);
            var transitionCount = decorationOverlays.Count(kvp => kvp.Value.Bitmask > 0 && kvp.Value.Bitmask < 15);
            ILog.Print($"Dual-grid terrain: {decorationOverlays.Count} visual tiles, {transitionCount} transitions");

            // Validate bitmask consistency and log any remaining issues
            ValidateBitmaskConsistency(decorationOverlays, size.X + 1, size.Y + 1);
        }

        // Copy terrain to final grid
        var finalTileIds = new string[size.Y, size.X];
        Array.Copy(terrainGrid, finalTileIds, terrainGrid.Length);

        // Select contextual variants (empty - variant system removed)
        var contextualVariants = new Dictionary<Vector2I, int>();

        // Choose random positions for player and enemies
        var shuffledTiles = passableTiles.OrderBy(_ => _rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];

        // Place 2-3 enemies randomly
        var enemyCount = _rng.RandiRange(2, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print($"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, {enemyCount} enemies");

        // Optional: Validate spatial coherence (diagnostic)
        ValidateSpatialCoherence(finalTileIds, size);

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
            DecorationOverlays = decorationOverlays
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

    /// <summary>
    /// Generate dual-grid terrain transition data.
    /// Visual grid is (size+1) x (size+1), offset by half a tile from data grid.
    /// Each visual tile samples 4 data corners, selects a topTerrain (highest-dominance
    /// auto-tile), and computes a bitmask indicating which corners contain that terrain.
    /// </summary>
    private Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> GenerateTerrainTransitions(
        string[,] terrainGrid, Vector2I size)
    {
        var visualWidth = size.X + 1;
        var visualHeight = size.Y + 1;
        var overlays = new Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>();

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var visualPosition = new Vector2I(vx, vy);
            var terrainInfo = new Dictionary<string, (int Dominance, bool HasAutoTile)>();

            SampleTerrainCell(terrainGrid, size, vx - 1, vy - 1, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx, vy - 1, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx - 1, vy, terrainInfo);
            SampleTerrainCell(terrainGrid, size, vx, vy, terrainInfo);

            var (baseTerrain, topTerrain) = SelectTerrains(terrainInfo);
            var bitmask = ComputeBitmask(terrainGrid, size, vx, vy, topTerrain);

            overlays[visualPosition] = (baseTerrain, topTerrain, bitmask);
        }

        return overlays;
    }

    /// <summary>
    /// Selects base and top terrains from the terrain info for a visual tile.
    /// </summary>
    private (string BaseTerrain, string TopTerrain) SelectTerrains(
        Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        string? baseTerrain = null;
        string? topTerrain = null;
        var baseDominance = int.MaxValue;
        var topDominance = -1;

        foreach (var (terrain, (dominance, hasAutoTile)) in terrainInfo)
        {
            if (hasAutoTile)
            {
                if (topTerrain == null ||
                    dominance > topDominance ||
                    (dominance == topDominance && string.CompareOrdinal(terrain, topTerrain) < 0))
                {
                    topTerrain = terrain;
                    topDominance = dominance;
                }
            }

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

        if (topTerrain == null)
        {
            topTerrain = baseTerrain ?? FloorTileId;
        }
        baseTerrain ??= topTerrain;

        return (baseTerrain, topTerrain);
    }

    /// <summary>
    /// Computes the Corner16 bitmask for a visual tile given the selected topTerrain.
    /// </summary>
    private static int ComputeBitmask(string[,] terrainGrid, Vector2I size, int vx, int vy, string topTerrain)
    {
        var bitmask = 0;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy - 1, topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthWest;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy - 1, topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthEast;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy, topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthWest;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy, topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthEast;
        return bitmask;
    }

    private void SampleTerrainCell(string[,] terrainGrid, Vector2I size, int x, int y,
        Dictionary<string, (int Dominance, bool HasAutoTile)> terrainInfo)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);

        var tileId = terrainGrid[y, x];
        var tile = _tileRegistry.GetTile(tileId);

        if (tile == null || tile.Layer != TileLayer.Terrain)
            return;

        if (!terrainInfo.ContainsKey(tileId))
        {
            terrainInfo[tileId] = (tile.Dominance, tile.HasAutoTileVariants);
        }
    }

    private static bool IsTerrainAtPosition(string[,] terrainGrid, Vector2I size, int x, int y, string terrainTileId)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);
        return terrainGrid[y, x] == terrainTileId;
    }

    /// <summary>
    /// Diagnostic: Analyzes spatial coherence by measuring contiguous region sizes.
    /// Reports region statistics for evaluating map quality.
    /// </summary>
    private void ValidateSpatialCoherence(string[,] tileMap, Vector2I size)
    {
        var metrics = RegionAnalyzer.Analyze(tileMap);

        ILog.Print($"[SpatialCoherence] {metrics.RegionCount} regions found (avg size: {metrics.AverageSize:F1} tiles)");
        ILog.Print($"[SpatialCoherence] Region sizes: min={metrics.MinSize}, max={metrics.MaxSize}");
        ILog.Print($"[SpatialCoherence] {metrics.PercentInLargeRegions:F1}% of tiles in regions >= 30 tiles ({metrics.TilesInLargeRegions}/{metrics.TotalTiles})");

        if (metrics.PercentInLargeRegions < 70.0f)
        {
            ILog.Print($"[SpatialCoherence] WARNING: Low coherence - only {metrics.PercentInLargeRegions:F1}% of tiles in large regions (target: 70%+)");
        }
    }

    /// <summary>
    /// Validates that adjacent visual tiles have consistent bitmasks.
    /// Logs warnings for any remaining inconsistencies after conflict resolution.
    /// </summary>
    private static void ValidateBitmaskConsistency(
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> overlays,
        int visualWidth, int visualHeight)
    {
        var (totalTiles, uniqueTerrains, terrainConflicts, bitmaskViolations) =
            BitmaskConsistencyValidator.Analyze(overlays, visualWidth, visualHeight);

        ILog.Print($"[BitmaskConsistency] {totalTiles} tiles, {uniqueTerrains} unique topTerrains");

        if (terrainConflicts > 0)
        {
            ILog.Print($"[BitmaskConsistency] {terrainConflicts} adjacent tiles with different topTerrains (3-way boundaries)");
        }

        if (bitmaskViolations > 0)
        {
            ILog.Print($"[BitmaskConsistency] WARNING: {bitmaskViolations} bitmask violations detected!");

            // Log first few violations for debugging
            var violations = BitmaskConsistencyValidator.ValidateConsistency(overlays, visualWidth, visualHeight);
            foreach (var v in violations.Take(3))
            {
                ILog.Print($"[BitmaskConsistency]   {v}");
            }
            if (violations.Count > 3)
            {
                ILog.Print($"[BitmaskConsistency]   ... and {violations.Count - 3} more");
            }
        }
        else
        {
            ILog.Print($"[BitmaskConsistency] All bitmasks consistent (same-terrain adjacencies match)");
        }
    }
}
