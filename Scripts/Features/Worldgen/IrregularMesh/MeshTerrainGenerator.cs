using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using TWO-PASS WFC.
/// Now runs WFC directly on mesh topology (vertices as cells, quad-sharing neighbors)
/// instead of projecting from rectangular grid.
/// Pass 1: Background layer (non-auto-tiles) - still uses rectangular grid for quads
/// Pass 2: Foreground layer (auto-tiles) - uses mesh topology for proper gap constraints
/// </summary>
public class MeshTerrainGenerator
{
    private readonly WfcMapGenerator _wfcGenerator;
    private readonly ITileRegistry _tileRegistry;
    private readonly Dictionary<string, int>? _tileToTerrainType;
    private readonly CompiledTransitionResolver? _transitionResolver;

    /// <summary>
    /// When true, uses mesh topology for foreground WFC (proper gap constraints).
    /// When false, uses legacy grid projection (for backward compatibility testing).
    /// </summary>
    public bool UseDirectMeshWfc { get; set; } = true;

    public int MaxRetries
    {
        get => _wfcGenerator.MaxRetries;
        set => _wfcGenerator.MaxRetries = value;
    }

    /// <summary>
    /// Creates a two-pass terrain generator (preferred constructor).
    /// </summary>
    public MeshTerrainGenerator(WfcMapGenerator wfcGenerator, ITileRegistry tileRegistry, CompiledTransitionResolver? transitionResolver = null)
    {
        _wfcGenerator = wfcGenerator;
        _tileRegistry = tileRegistry;
        _transitionResolver = transitionResolver;
    }

    /// <summary>
    /// Backward-compatible constructor for old code using adjacency rules directly.
    /// Creates a WfcMapGenerator internally.
    /// </summary>
    public MeshTerrainGenerator(
        WfcAdjacencyRules adjacencyRules,
        Dictionary<string, int> tileToTerrainType,
        ITileRegistry tileRegistry)
    {
        _wfcGenerator = new WfcMapGenerator(adjacencyRules, tileRegistry);
        _tileRegistry = tileRegistry;
        _tileToTerrainType = tileToTerrainType;
    }

    /// <summary>
    /// Even older backward-compatible constructor using raw dictionaries.
    /// </summary>
    public MeshTerrainGenerator(
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType)
    {
        // Convert raw dictionaries to WfcAdjacencyRules
        var wfcRules = new WfcAdjacencyRules(new CompiledTransitionResolver());
        foreach (var (tile, neighbors) in adjacencyRules)
        {
            foreach (var neighbor in neighbors)
            {
                wfcRules.AddAdjacency(tile, neighbor);
            }
        }

        _wfcGenerator = new WfcMapGenerator(wfcRules, null);
        _tileRegistry = null!; // Will fail at runtime if registry is needed
        _tileToTerrainType = tileToTerrainType;

        GD.PrintErr("[MeshTerrainGen] Using legacy constructor without tile registry - two-pass WFC will not work!");
    }

    /// <summary>
    /// Generates terrain using two-pass WFC matching SimpleMapGenerator.
    /// </summary>
    public IrregularMesh Generate(
        int rings,
        BiomeRegistry biomeRegistry,
        Func<Vector2I, BiomeDefinition> getBiomeAt,
        ulong seed,
        int relaxationIterations = 15)
    {
        // Generate mesh geometry
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = (int)seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);

        // Calculate effective grid size from mesh bounds
        var bounds = mesh.Bounds;
        var effectiveSize = new Vector2I(
            (int)Math.Ceiling(bounds.Max.X - bounds.Min.X),
            (int)Math.Ceiling(bounds.Max.Y - bounds.Min.Y)
        );

        GD.Print($"[MeshTerrainGen] Mesh: {mesh.Vertices.Count} vertices, {mesh.Quads.Count} quads, effective size: {effectiveSize}");

        // PASS 1: Background layer (non-auto-tiles only)
        var bgResult = _wfcGenerator.GenerateMultiBiome(
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            seed,
            null, // No gradient for now
            tile => !tile.HasAutoTileVariants  // ONLY non-auto-tiles
        );

        if (!bgResult.Success || bgResult.MapData == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Background WFC failed: {bgResult.ErrorMessage}");
            ApplyFallbackTerrain(mesh);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Background WFC succeeded in {bgResult.Iterations} iterations");

        // Map background to quads
        MapBackgroundToQuads(mesh, bgResult.MapData, bounds);

        // PASS 2: Foreground layer (auto-tiles)
        if (UseDirectMeshWfc)
        {
            // Use direct mesh topology for proper gap constraints
            var fgSuccess = GenerateForegroundOnMesh(mesh, biomeRegistry, seed + 1);
            if (!fgSuccess)
            {
                GD.PrintErr("[MeshTerrainGen] Direct mesh foreground WFC failed, clearing foreground");
                ClearForeground(mesh);
            }
        }
        else
        {
            // Legacy: project from rectangular grid
            var fgResult = _wfcGenerator.GenerateMultiBiome(
                biomeRegistry,
                getBiomeAt,
                effectiveSize,
                seed + 1,
                null
            );

            if (!fgResult.Success || fgResult.MapData == null)
            {
                GD.PrintErr($"[MeshTerrainGen] Foreground WFC failed: {fgResult.ErrorMessage}");
                ClearForeground(mesh);
            }
            else
            {
                GD.Print($"[MeshTerrainGen] Legacy foreground WFC succeeded in {fgResult.Iterations} iterations");
                MapForegroundToVertices(mesh, fgResult.MapData, bounds);
            }
        }

        mesh.UpdateAllCachedProperties();
        return mesh;
    }

    /// <summary>
    /// Backward-compatible Generate method for old code (single biome).
    /// </summary>
    public IrregularMesh Generate(
        int rings,
        BiomeDefinition? biome = null,
        int seed = 0,
        int relaxationIterations = 15)
    {
        if (biome == null)
        {
            // Fallback to simple generation
            return GenerateFallback(rings, seed, relaxationIterations);
        }

        // Create a registry with just this biome
        var registry = new BiomeRegistry();
        // Note: This won't actually work without the biome being registered
        // This is just for backward compatibility

        return Generate(rings, registry, _ => biome, (ulong)seed, relaxationIterations);
    }

    /// <summary>
    /// Optional biome registry for card-based generation.
    /// Set via SetBiomeRegistry before calling GenerateWithCards.
    /// </summary>
    private BiomeRegistry? _biomeRegistry;

    /// <summary>
    /// Sets the biome registry for card-based terrain generation.
    /// </summary>
    public void SetBiomeRegistry(BiomeRegistry registry)
    {
        _biomeRegistry = registry;
    }

    /// <summary>
    /// Generates terrain using card signatures to influence biome distribution.
    /// Uses 2-phase WFC with card-based gradient for biome selection.
    /// </summary>
    public IrregularMesh GenerateWithCards(
        int rings,
        CardSignature[] inputCards,
        int seed,
        int relaxationIterations = 15,
        bool useConstraints = true)
    {
        // Generate mesh geometry first
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);

        // Check if we have what we need for proper WFC
        if (_biomeRegistry == null || _biomeRegistry.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No biomes registered, using fallback terrain");
            ApplyFallbackTerrain(mesh);
            return mesh;
        }

        if (inputCards.Length == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No input cards, using fallback terrain");
            ApplyFallbackTerrain(mesh);
            return mesh;
        }

        // Create card-based gradient for biome selection
        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)seed;
        var gradient = new CardBasedGradient(inputCards, rng);

        // Calculate effective grid size from mesh bounds
        var bounds = mesh.Bounds;
        var effectiveSize = new Vector2I(
            (int)Math.Ceiling(bounds.Max.X - bounds.Min.X),
            (int)Math.Ceiling(bounds.Max.Y - bounds.Min.Y)
        );

        // Create biome provider that maps positions to biomes using card gradient
        BiomeDefinition GetBiomeAt(Vector2I pos)
        {
            var signature = gradient.GetSignatureAt(pos, effectiveSize);
            return _biomeRegistry.FindClosestBySignature(signature)
                ?? _biomeRegistry.GetAllBiomes().First();
        }

        GD.Print($"[MeshTerrainGen] Generating with {inputCards.Length} cards, {_biomeRegistry.Count} biomes, effective size: {effectiveSize}");

        // PASS 1: Background layer (non-auto-tiles only)
        var bgResult = _wfcGenerator.GenerateMultiBiome(
            _biomeRegistry,
            GetBiomeAt,
            effectiveSize,
            (ulong)seed,
            null,
            tile => !tile.HasAutoTileVariants
        );

        if (!bgResult.Success || bgResult.MapData == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Background WFC failed: {bgResult.ErrorMessage}");
            ApplyFallbackTerrain(mesh);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Background WFC succeeded in {bgResult.Iterations} iterations");

        // Map background to quads
        MapBackgroundToQuads(mesh, bgResult.MapData, bounds);

        // PASS 2: Foreground layer (auto-tiles)
        if (UseDirectMeshWfc)
        {
            // Use direct mesh topology for proper gap constraints
            var fgSuccess = GenerateForegroundOnMesh(mesh, _biomeRegistry, (ulong)seed + 1);
            if (!fgSuccess)
            {
                GD.PrintErr("[MeshTerrainGen] Direct mesh foreground WFC failed, clearing foreground");
                ClearForeground(mesh);
            }
        }
        else
        {
            // Legacy: project from rectangular grid
            var fgResult = _wfcGenerator.GenerateMultiBiome(
                _biomeRegistry,
                GetBiomeAt,
                effectiveSize,
                (ulong)seed + 1,
                null
            );

            if (!fgResult.Success || fgResult.MapData == null)
            {
                GD.PrintErr($"[MeshTerrainGen] Foreground WFC failed: {fgResult.ErrorMessage}");
                ClearForeground(mesh);
            }
            else
            {
                GD.Print($"[MeshTerrainGen] Legacy foreground WFC succeeded in {fgResult.Iterations} iterations");
                MapForegroundToVertices(mesh, fgResult.MapData, bounds);
            }
        }

        mesh.UpdateAllCachedProperties();
        return mesh;
    }

    private IrregularMesh GenerateFallback(int rings, int seed, int relaxationIterations)
    {
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);
        ApplyFallbackTerrain(mesh);
        return mesh;
    }

    /// <summary>
    /// Maps background WFC results to quad faces using centroid position.
    /// </summary>
    private void MapBackgroundToQuads(
        IrregularMesh mesh,
        SimpleMapData backgroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        var wfcSize = backgroundData.Size;
        var meshSize = bounds.Max - bounds.Min;
        var minPos = bounds.Min;

        foreach (var quad in mesh.Quads)
        {
            var normalizedX = (quad.Centroid.X - minPos.X) / meshSize.X;
            var normalizedY = (quad.Centroid.Y - minPos.Y) / meshSize.Y;

            var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
            var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);

            quad.BackgroundTileId = backgroundData.TileIds[gridY, gridX];
        }

        GD.Print($"[MeshTerrainGen] Mapped {mesh.Quads.Count} quads with backgrounds");
    }

    /// <summary>
    /// Maps foreground WFC results to vertices using position projection (legacy method).
    /// </summary>
    private void MapForegroundToVertices(
        IrregularMesh mesh,
        SimpleMapData foregroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        var wfcSize = foregroundData.Size;
        var meshSize = bounds.Max - bounds.Min;
        var minPos = bounds.Min;
        var autoTileCount = 0;

        foreach (var vertex in mesh.Vertices)
        {
            var normalizedX = (vertex.Position.X - minPos.X) / meshSize.X;
            var normalizedY = (vertex.Position.Y - minPos.Y) / meshSize.Y;

            var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
            var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);

            var fgTileId = foregroundData.TileIds[gridY, gridX];
            var fgTile = _tileRegistry?.GetTile(fgTileId);

            if (fgTile?.HasAutoTileVariants == true)
            {
                vertex.ForegroundTileId = fgTileId;
                vertex.TileId = fgTileId;
                autoTileCount++;
            }
            else
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
            }

            vertex.TerrainType = fgTile?.IsPassable == true ? 1 : 0;
        }

        GD.Print($"[MeshTerrainGen] Mapped {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles (legacy projection)");
    }

    /// <summary>
    /// Clears foreground from all vertices.
    /// </summary>
    private void ClearForeground(IrregularMesh mesh)
    {
        foreach (var vertex in mesh.Vertices)
        {
            vertex.ForegroundTileId = null;
            vertex.TileId = null;
            vertex.TerrainType = 1; // Default passable
        }
    }

    /// <summary>
    /// Maps two-pass WFC results to mesh (legacy combined method).
    /// </summary>
    private void MapTwoPassWfcToMesh(
        IrregularMesh mesh,
        SimpleMapData backgroundData,
        SimpleMapData foregroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        MapBackgroundToQuads(mesh, backgroundData, bounds);
        MapForegroundToVertices(mesh, foregroundData, bounds);
        mesh.UpdateAllCachedProperties();
    }

    /// <summary>
    /// Maps single-pass WFC to mesh (fallback when only one pass succeeds).
    /// Background is mapped to quads, no foreground is assigned.
    /// </summary>
    private void MapWfcToVertices(
        IrregularMesh mesh,
        SimpleMapData wfcData,
        (Vector2 Min, Vector2 Max) bounds,
        bool isBackgroundOnly)
    {
        var wfcSize = wfcData.Size;
        var meshSize = bounds.Max - bounds.Min;
        var minPos = bounds.Min;

        // Map background to quads
        foreach (var quad in mesh.Quads)
        {
            var normalizedX = (quad.Centroid.X - minPos.X) / meshSize.X;
            var normalizedY = (quad.Centroid.Y - minPos.Y) / meshSize.Y;

            var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
            var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);

            quad.BackgroundTileId = wfcData.TileIds[gridY, gridX];
        }

        // No foreground for vertices in single-pass mode
        foreach (var vertex in mesh.Vertices)
        {
            vertex.ForegroundTileId = null;
            vertex.TileId = null;
            vertex.TerrainType = 1; // Default passable
        }

        mesh.UpdateAllCachedProperties();

        GD.Print($"[MeshTerrainGen] Mapped {mesh.Quads.Count} quads ({(isBackgroundOnly ? "background only" : "single pass")})");
    }

    private int DetermineTerrainType(string tileId, TileDefinition? tile)
    {
        // Try tile registry first
        if (tile != null)
        {
            return tile.IsPassable ? 1 : 0;
        }

        // Fall back to tileToTerrainType mapping if available
        if (_tileToTerrainType != null && _tileToTerrainType.TryGetValue(tileId, out var terrainType))
        {
            return terrainType;
        }

        // Ultimate fallback: assume passable
        return 1;
    }

    private void ApplyFallbackTerrain(IrregularMesh mesh)
    {
        // Find any passable tile as fallback
        var fallbackTile = _tileRegistry?.GetAllTiles()
            .FirstOrDefault(t => t.IsPassable && !t.HasAutoTileVariants);

        var fallbackId = fallbackTile?.Id ?? "floor";
        var fallbackTerrainType = 1; // Assume passable

        // Try to get terrain type from mapping if available
        if (_tileToTerrainType != null && _tileToTerrainType.TryGetValue(fallbackId, out var mappedType))
        {
            fallbackTerrainType = mappedType;
        }

        // Apply fallback background to all quads
        foreach (var quad in mesh.Quads)
        {
            quad.BackgroundTileId = fallbackId;
        }

        // Clear foreground from all vertices
        foreach (var vertex in mesh.Vertices)
        {
            vertex.TileId = null;
            vertex.ForegroundTileId = null;
            vertex.TerrainType = fallbackTerrainType;
        }

        mesh.UpdateAllCachedProperties();
        GD.PrintErr("[MeshTerrainGen] Applied fallback terrain");
    }

    /// <summary>
    /// Runs WFC directly on the mesh topology for foreground (auto-tile) generation.
    /// This ensures gap constraints are properly enforced using quad-sharing neighbors.
    /// </summary>
    private bool GenerateForegroundOnMesh(
        IrregularMesh mesh,
        BiomeRegistry biomeRegistry,
        ulong seed)
    {
        if (_tileRegistry == null)
        {
            GD.PrintErr("[MeshTerrainGen] Cannot run direct mesh WFC without tile registry");
            return false;
        }

        // Collect initial tiles (all auto-tiles and gap tiles)
        var initialTiles = new HashSet<string>();
        var biomeIds = biomeRegistry.GetAllBiomeIds().ToList();

        foreach (var tile in _tileRegistry.GetAllTiles())
        {
            // Check if tile is allowed in any biome
            var isAllowedInAnyBiome = biomeIds.Any(biomeId => tile.IsAllowedInBiome(biomeId));
            if (!isAllowedInAnyBiome)
                continue;

            initialTiles.Add(tile.Id);
        }

        if (initialTiles.Count == 0)
        {
            GD.PrintErr("[MeshTerrainGen] No valid tiles for mesh WFC");
            return false;
        }

        GD.Print($"[MeshTerrainGen] Starting direct mesh WFC with {initialTiles.Count} tiles on {mesh.Vertices.Count} vertices");

        // Create mesh topology
        var topology = new IrregularMeshWfcTopology(mesh, initialTiles);

        // Create propagator with adjacency rules (if available)
        var adjacencyRules = _transitionResolver != null
            ? new WfcAdjacencyRules(_transitionResolver)
            : new WfcAdjacencyRules(new CompiledTransitionResolver());

        // Allow all gap tiles to be adjacent to each other and to auto-tiles
        ConfigureGapTileAdjacencies(adjacencyRules);

        var propagator = new WfcPropagator(adjacencyRules);

        // Create selector with constraints matching background layer
        var selector = new WfcTileSelector();

        // Create blob tracker for diminishing returns
        var blobTracker = new BlobSizeTracker();
        blobTracker.Initialize(mesh.Vertices.Count);

        // Diminishing returns to prevent single tile dominating the map
        var diminishingReturns = new DiminishingReturnsSoftModifier(blobTracker);
        selector.AddConstraint(diminishingReturns);

        // Spatial coherence for region formation (critical for clustering)
        var spatialCoherence = new SpatialCoherenceConstraint(_tileRegistry);
        selector.AddConstraint(spatialCoherence);

        // Gap constraint to prevent different auto-tiles from being adjacent
        selector.AddConstraint(new AutoTileGapConstraint(_tileRegistry));

        // Prevent 2x2 solid regions for tilesets lacking bitmask 15
        selector.AddConstraint(new NoSolidFillConstraint(_tileRegistry));

        // Apply tile probability/density from TSX and variation groups
        if (_tileRegistry is TileRegistry concreteRegistry)
        {
            selector.AddConstraint(new TileProbabilityConstraint(concreteRegistry));
        }

        // Create solver with blob tracking and spatial coherence
        var solver = new WfcSolver(propagator, selector, blobTracker, spatialCoherence: spatialCoherence, tileRegistry: _tileRegistry);
        solver.MaxIterations = mesh.Vertices.Count * 2; // Allow reasonable iterations

        // Run WFC
        var rng = new RandomNumberGenerator();
        rng.Seed = seed;

        var result = solver.Solve(topology, null, rng);

        if (!result.Success)
        {
            GD.PrintErr($"[MeshTerrainGen] Direct mesh WFC failed: {result.ErrorMessage}");
            return false;
        }

        GD.Print($"[MeshTerrainGen] Direct mesh WFC succeeded in {result.Iterations} iterations");

        // Map results to vertices
        var autoTileCount = 0;
        for (var i = 0; i < mesh.Vertices.Count; i++)
        {
            var tileId = topology.GetCollapsedTileAt(i);
            var vertex = mesh.Vertices[i];

            if (tileId == null)
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
                vertex.TerrainType = 1;
                continue;
            }

            var tile = _tileRegistry.GetTile(tileId);

            // Only assign foreground if it's an auto-tile
            if (tile?.HasAutoTileVariants == true)
            {
                vertex.ForegroundTileId = tileId;
                vertex.TileId = tileId;
                autoTileCount++;
            }
            else
            {
                vertex.ForegroundTileId = null;
                vertex.TileId = null;
            }

            // Set terrain type for passability
            vertex.TerrainType = tile?.IsPassable == true ? 1 : 0;
        }

        GD.Print($"[MeshTerrainGen] Assigned {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles");
        return true;
    }

    /// <summary>
    /// Configures adjacency rules so gap tiles can be adjacent to anything.
    /// </summary>
    private void ConfigureGapTileAdjacencies(WfcAdjacencyRules adjacencyRules)
    {
        if (_tileRegistry == null)
            return;

        var gapTiles = new List<string>();
        var autoTiles = new List<string>();

        foreach (var tile in _tileRegistry.GetAllTiles())
        {
            if (!tile.HasAutoTileVariants)
                gapTiles.Add(tile.Id);
            else
                autoTiles.Add(tile.Id);
        }

        // Gap tiles can be adjacent to each other
        if (gapTiles.Count > 0)
        {
            adjacencyRules.AddMutualAdjacencies(gapTiles);

            // Gap tiles can be adjacent to any auto-tile
            foreach (var gapTile in gapTiles)
            {
                foreach (var autoTile in autoTiles)
                {
                    adjacencyRules.AddAdjacency(gapTile, autoTile);
                }
            }
        }

        // Auto-tiles can be adjacent to themselves
        foreach (var autoTile in autoTiles)
        {
            adjacencyRules.EnsureSelfAdjacency(autoTile);
        }
    }
}
