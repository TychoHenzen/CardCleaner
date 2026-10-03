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
    /// <summary>
    /// Maximum WFC retry attempts on contradiction (default 5).
    /// </summary>
    public int MaxWfcRetries { get; set; } = 5;

    private readonly IBiomeProvider _biomeProvider;
    private readonly RandomNumberGenerator _rng;
    private readonly ITileRegistry _tileRegistry;
    private readonly ITileMetadataProvider _metadataProvider;
    private readonly WfcMapGenerator? _wfcGenerator;
    private readonly BiomeRegistry? _biomeRegistry;
    private readonly BaselineGradient? _gradient;
    private IProfiler _profiler = new NoOpProfiler();

    // Cached default tile IDs (resolved once at construction)
    private readonly string _defaultPassableTileId;
    private readonly string _defaultSolidTileId;

    public SimpleMapGenerator(RandomNumberGenerator rng, IBiomeProvider biomeProvider, ITileRegistry tileRegistry,
        ITileMetadataProvider metadataProvider,
        WfcMapGenerator? wfcGenerator = null, BiomeRegistry? biomeRegistry = null,
        BaselineGradient? gradient = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        ArgumentNullException.ThrowIfNull(metadataProvider);
        _rng = rng;
        _biomeProvider = biomeProvider;
        _tileRegistry = tileRegistry;
        _metadataProvider = metadataProvider;
        _wfcGenerator = wfcGenerator;
        _biomeRegistry = biomeRegistry;
        _gradient = gradient;

        // Cache default tile IDs for fallbacks
        _defaultPassableTileId = metadataProvider.GetDefaultPassableTileId() ?? "floor";
        _defaultSolidTileId = metadataProvider.GetDefaultSolidTileId() ?? "wall";
    }

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
        _wfcGenerator?.SetProfiler(profiler);
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        ILog.Print($"Generating WFC-based map {size.X}x{size.Y}");

        // Pre-select per-generation variants (legacy atlas coordinate variations)
        Dictionary<string, int> perGenerationVariants;
        Dictionary<string, string> perGenerationGroupVariants;
        using (_profiler.BeginScope("VariantSelection"))
        {
            perGenerationVariants = SelectPerGenerationVariants();
            perGenerationGroupVariants = SelectPerGenerationGroupVariants();

            // Pass selected variants to WFC so non-selected variants are excluded
            _wfcGenerator?.SetSelectedVariants(perGenerationGroupVariants);
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

        // Generate terrain using two-phase WFC
        string[,] backgroundLayer;
        string[,] foregroundLayer;
        string[,] terrainGrid;
        using (_profiler.BeginScope("TwoPhaseWfcGeneration"))
        {
            var twoPhaseResult = GenerateTwoPhaseWfc(size, biomeMap);
            backgroundLayer = twoPhaseResult.BackgroundLayer;
            foregroundLayer = twoPhaseResult.ForegroundLayer;
            terrainGrid = twoPhaseResult.MergedGrid;
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
                terrainGrid[center.Y, center.X] = _defaultPassableTileId;
                passableTiles.Add(center);
            }
        }

        // Generate terrain transitions BEFORE placing structures
        Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> decorationOverlays;
        using (_profiler.BeginScope("TerrainTransitions"))
        {
            decorationOverlays = GenerateTerrainTransitions(backgroundLayer, foregroundLayer, size);
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
            BackgroundLayer = backgroundLayer,
            ForegroundLayer = foregroundLayer,
            BiomeMap = biomeMap,
            Size = size,
            PlayerStart = playerStart,
            EnemyPositions = enemyPositions,
            PassableTiles = passableTiles,
            PerGenerationVariants = perGenerationVariants,
            PerGenerationGroupVariants = perGenerationGroupVariants,
            ContextualVariants = contextualVariants,
            DecorationOverlays = decorationOverlays
        };
    }

    /// <summary>
    /// Generates terrain using two-phase WFC:
    /// Phase 1: Background layer (simple/non-auto tiles)
    /// Phase 2: Foreground layer (auto-tiles with gap constraint)
    /// Both layers are WFC-generated and used together for transitions.
    /// </summary>
    private TwoPhaseWfcResult GenerateTwoPhaseWfc(
        Vector2I size, string[,] biomeMap)
    {
        var backgroundLayer = new string[size.Y, size.X];
        var foregroundLayer = new string[size.Y, size.X];
        var mergedGrid = new string[size.Y, size.X];

        if (_wfcGenerator == null || _biomeRegistry == null)
        {
            ILog.Print("[WFC] No WFC generator, using random fallback");
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var biome = _biomeProvider.GetBiomeAt(new Vector2I(x, y));
                var tile = biome.SelectPassableTile(_rng) ?? _defaultPassableTileId;
                backgroundLayer[y, x] = tile;
                foregroundLayer[y, x] = "";
                mergedGrid[y, x] = tile;
            }
            return new TwoPhaseWfcResult(backgroundLayer, foregroundLayer, mergedGrid);
        }

        _wfcGenerator.MaxRetries = MaxWfcRetries;

        // Phase 1: Generate background layer with simple (non-auto) tiles only
        var bgResult = _wfcGenerator.GenerateMultiBiome(
            _biomeRegistry,
            pos => _biomeProvider.GetBiomeAt(pos),
            size,
            _rng.Randi(),
            _gradient,
            tile => !tile.HasAutoTileVariants);  // Only simple tiles

        if (bgResult.Success && bgResult.MapData != null)
        {
            ILog.Print($"[WFC] Background phase succeeded in {bgResult.Iterations} iterations");
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                backgroundLayer[y, x] = bgResult.MapData.TileIds[y, x];
            }
        }
        else
        {
            ILog.Print($"[WFC] Background phase failed: {bgResult.ErrorMessage}, using default passable fallback");
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                backgroundLayer[y, x] = _defaultPassableTileId;
            }
        }

        // Phase 2: Generate foreground layer - all tiles, then keep only auto-tiles
        // WFC needs gap tiles (simple tiles) to fill spaces between auto-tile regions
        // The gap constraint ensures different auto-tiles don't touch directly
        var fgResult = _wfcGenerator.GenerateMultiBiome(
            _biomeRegistry,
            pos => _biomeProvider.GetBiomeAt(pos),
            size,
            _rng.Randi(),
            _gradient);  // No filter - include both auto-tiles and gap tiles

        if (fgResult.Success && fgResult.MapData != null)
        {
            ILog.Print($"[WFC] Foreground phase succeeded in {fgResult.Iterations} iterations");
            var autoTileCount = 0;
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                var tileId = fgResult.MapData.TileIds[y, x];
                var tile = _tileRegistry.GetTile(tileId);
                // Only keep auto-tiles in foreground; non-auto tiles become empty (show background)
                if (tile?.HasAutoTileVariants == true)
                {
                    foregroundLayer[y, x] = tileId;
                    autoTileCount++;
                }
                else
                {
                    foregroundLayer[y, x] = "";
                }
            }
            ILog.Print($"[WFC] Foreground contains {autoTileCount} auto-tile cells");
        }
        else
        {
            ILog.Print($"[WFC] Foreground phase failed: {fgResult.ErrorMessage}, using empty foreground");
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
            {
                foregroundLayer[y, x] = "";
            }
        }

        // Merge: foreground takes precedence where present
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            mergedGrid[y, x] = !string.IsNullOrEmpty(foregroundLayer[y, x])
                ? foregroundLayer[y, x]
                : backgroundLayer[y, x];
        }

        return new TwoPhaseWfcResult(backgroundLayer, foregroundLayer, mergedGrid);
    }

    private bool IsPassableTile(string tileId)
    {
        var tile = _tileRegistry.GetTile(tileId);
        return tile?.IsPassable ?? false;
    }

    private Dictionary<string, int> SelectPerGenerationVariants()
    {
        var variants = new Dictionary<string, int>();

        // Legacy support: tiles with built-in Variations array
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
    /// Selects one tile from each PerGeneration variation group.
    /// Returns a mapping from base name to selected tile ID.
    /// </summary>
    private Dictionary<string, string> SelectPerGenerationGroupVariants()
    {
        var selected = new Dictionary<string, string>();

        foreach (var group in _tileRegistry.GetAllVariationGroups())
        {
            if (group.Mode != VariationMode.PerGeneration)
                continue;

            var selectedTileId = _tileRegistry.SelectPerMapVariant(group.BaseName, _rng);
            if (selectedTileId != null)
            {
                selected[group.BaseName] = selectedTileId;
                ILog.Print($"[SimpleMapGenerator] Selected '{selectedTileId}' for variation group '{group.BaseName}'");
            }
        }

        return selected;
    }

    /// <summary>
    /// Generate dual-grid terrain transition data.
    /// Visual grid is (size+1) x (size+1), offset by half a tile from data grid.
    /// Each visual tile samples 4 data corners, selects a topTerrain (highest-dominance
    /// auto-tile), and computes a bitmask indicating which corners contain that terrain.
    /// </summary>
    private Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)> GenerateTerrainTransitions(
        string[,] backgroundLayer, string[,] foregroundLayer, Vector2I size)
    {
        var visualWidth = size.X + 1;
        var visualHeight = size.Y + 1;
        var overlays = new Dictionary<Vector2I, (string BaseTileId, string TopTileId, int Bitmask)>();

        for (var vy = 0; vy < visualHeight; vy++)
        for (var vx = 0; vx < visualWidth; vx++)
        {
            var visualPosition = new Vector2I(vx, vy);

            // Sample the 4 corners from foreground (auto-tiles)
            var fgNW = GetCellSafe(foregroundLayer, size, vx - 1, vy - 1);
            var fgNE = GetCellSafe(foregroundLayer, size, vx, vy - 1);
            var fgSW = GetCellSafe(foregroundLayer, size, vx - 1, vy);
            var fgSE = GetCellSafe(foregroundLayer, size, vx, vy);

            // Sample the 4 corners from background (simple tiles)
            var bgNW = GetCellSafe(backgroundLayer, size, vx - 1, vy - 1);
            var bgNE = GetCellSafe(backgroundLayer, size, vx, vy - 1);
            var bgSW = GetCellSafe(backgroundLayer, size, vx - 1, vy);
            var bgSE = GetCellSafe(backgroundLayer, size, vx, vy);

            var foregroundCorners = new[] { fgNW, fgNE, fgSW, fgSE };
            var backgroundCorners = new[] { bgNW, bgNE, bgSW, bgSE };

            // Find the auto-tile in this window (should be at most one type due to gap constraint)
            string? topTerrain = FindDominantAutoTile(foregroundCorners);

            string? baseTerrain = FindDominantBaseTile(backgroundCorners);

            // Fallback to default passable tile if no base terrain found
            baseTerrain ??= _defaultPassableTileId;
            topTerrain ??= baseTerrain;

            // Compute bitmask based on which corners have the same terrain type as topTerrain
            // Use AreSameTerrainType to handle variations (e.g., grass1 vs grass2)
            // Use Corner16 format: NE=1, SE=2, SW=4, NW=8 (dual-grid samples exactly 4 corners)
            var bitmask = ComputeTransitionBitmask(foregroundCorners, topTerrain);

            overlays[visualPosition] = (baseTerrain, topTerrain, bitmask);
        }

        return overlays;
    }

    private string? FindDominantAutoTile(IReadOnlyList<string> corners)
    {
        string? topTerrain = null;
        var topDominance = -1;

        foreach (var corner in corners)
        {
            if (string.IsNullOrEmpty(corner)) continue;
            var tile = _tileRegistry.GetTile(corner);
            if (tile?.HasAutoTileVariants == true && tile.Dominance > topDominance)
            {
                topTerrain = corner;
                topDominance = tile.Dominance;
            }
        }

        return topTerrain;
    }

    private string? FindDominantBaseTile(IReadOnlyList<string> corners)
    {
        string? baseTerrain = null;
        var baseDominance = int.MaxValue;

        foreach (var corner in corners)
        {
            if (string.IsNullOrEmpty(corner)) continue;
            var tile = _tileRegistry.GetTile(corner);
            if (tile != null && tile.Dominance < baseDominance)
            {
                baseTerrain = corner;
                baseDominance = tile.Dominance;
            }
        }

        return baseTerrain;
    }

    private int ComputeTransitionBitmask(IReadOnlyList<string> foregroundCorners, string topTerrain)
    {
        var bitmask = 0;
        if (_tileRegistry.AreSameTerrainType(foregroundCorners[1], topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthEast;
        if (_tileRegistry.AreSameTerrainType(foregroundCorners[3], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthEast;
        if (_tileRegistry.AreSameTerrainType(foregroundCorners[2], topTerrain))
            bitmask |= NeighborBitmaskCorner.SouthWest;
        if (_tileRegistry.AreSameTerrainType(foregroundCorners[0], topTerrain))
            bitmask |= NeighborBitmaskCorner.NorthWest;
        return bitmask;
    }

    private static string GetCellSafe(string[,] grid, Vector2I size, int x, int y)
    {
        x = Math.Clamp(x, 0, size.X - 1);
        y = Math.Clamp(y, 0, size.Y - 1);
        return grid[y, x];
    }

    /// <summary>
    /// Selects a simple (non-auto-tile) passable tile allowed in the biome.
    /// Used as background for auto-tiles.
    /// </summary>
    private string? SelectSimpleTileFromBiome(BiomeDefinition biome)
    {
        // Find tiles without auto-tile variants that are allowed in this biome
        foreach (var tile in _tileRegistry.GetAllTiles())
        {
            if (tile.IsPassable && !tile.HasAutoTileVariants && tile.IsAllowedInBiome(biome.Id))
                return tile.Id;
        }

        return null;
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
            topTerrain = baseTerrain ?? _defaultPassableTileId;
        }
        baseTerrain ??= topTerrain;

        return (baseTerrain, topTerrain);
    }

    /// <summary>
    /// Computes the Full8/Blob47 bitmask for a visual tile given the selected topTerrain.
    /// </summary>
    private static int ComputeBitmask(string[,] terrainGrid, Vector2I size, int vx, int vy, string topTerrain)
    {
        // Use Corner16 format: NE=1, SE=2, SW=4, NW=8
        var bitmask = 0;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy - 1, topTerrain)) bitmask |= NeighborBitmaskCorner.NorthEast;
        if (IsTerrainAtPosition(terrainGrid, size, vx, vy, topTerrain)) bitmask |= NeighborBitmaskCorner.SouthEast;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy, topTerrain)) bitmask |= NeighborBitmaskCorner.SouthWest;
        if (IsTerrainAtPosition(terrainGrid, size, vx - 1, vy - 1, topTerrain)) bitmask |= NeighborBitmaskCorner.NorthWest;
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
