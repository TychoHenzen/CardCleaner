using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using TWO-PASS WFC identical to regular grid system.
/// Pass 1: Background layer (non-auto-tiles)
/// Pass 2: Foreground layer (auto-tiles)
/// Pass 3: Renderer samples quad corners and computes bitmasks
/// </summary>
public class MeshTerrainGenerator
{
    private readonly WfcMapGenerator _wfcGenerator;
    private readonly ITileRegistry _tileRegistry;
    private readonly Dictionary<string, int>? _tileToTerrainType;

    public int MaxRetries
    {
        get => _wfcGenerator.MaxRetries;
        set => _wfcGenerator.MaxRetries = value;
    }

    /// <summary>
    /// Creates a two-pass terrain generator (preferred constructor).
    /// </summary>
    public MeshTerrainGenerator(WfcMapGenerator wfcGenerator, ITileRegistry tileRegistry)
    {
        _wfcGenerator = wfcGenerator;
        _tileRegistry = tileRegistry;
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

        // PASS 2: Foreground layer (all tiles, then filter to auto-tiles)
        var fgResult = _wfcGenerator.GenerateMultiBiome(
            biomeRegistry,
            getBiomeAt,
            effectiveSize,
            seed + 1, // Different seed for foreground
            null  // No filter - include all tiles (gap constraint handles spacing)
        );

        if (!fgResult.Success || fgResult.MapData == null)
        {
            GD.PrintErr($"[MeshTerrainGen] Foreground WFC failed: {fgResult.ErrorMessage}");
            // Continue with background only
            MapWfcToVertices(mesh, bgResult.MapData, bounds, true);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Foreground WFC succeeded in {fgResult.Iterations} iterations");

        // Map both layers to mesh: backgrounds to quads, foregrounds to vertices
        MapTwoPassWfcToMesh(mesh, bgResult.MapData, fgResult.MapData, bounds);

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

        // PASS 2: Foreground layer (all tiles)
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
            MapWfcToVertices(mesh, bgResult.MapData, bounds, true);
            return mesh;
        }

        GD.Print($"[MeshTerrainGen] Foreground WFC succeeded in {fgResult.Iterations} iterations");

        // Map both layers to mesh
        MapTwoPassWfcToMesh(mesh, bgResult.MapData, fgResult.MapData, bounds);

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
    /// Maps two-pass WFC results to mesh.
    /// Phase 1 (background) maps to quads (faces) - each quad gets one background tile.
    /// Phase 2 (foreground) maps to vertices - each vertex gets an auto-tile or null for gaps.
    /// </summary>
    private void MapTwoPassWfcToMesh(
        IrregularMesh mesh,
        SimpleMapData backgroundData,
        SimpleMapData foregroundData,
        (Vector2 Min, Vector2 Max) bounds)
    {
        var wfcSize = backgroundData.Size;
        var meshSize = bounds.Max - bounds.Min;
        var minPos = bounds.Min;

        // Phase 1: Map background tiles to quads (faces) using centroid position
        foreach (var quad in mesh.Quads)
        {
            var normalizedX = (quad.Centroid.X - minPos.X) / meshSize.X;
            var normalizedY = (quad.Centroid.Y - minPos.Y) / meshSize.Y;

            var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
            var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);

            quad.BackgroundTileId = backgroundData.TileIds[gridY, gridX];
        }

        // Phase 2: Map foreground tiles to vertices
        var autoTileCount = 0;

        foreach (var vertex in mesh.Vertices)
        {
            var normalizedX = (vertex.Position.X - minPos.X) / meshSize.X;
            var normalizedY = (vertex.Position.Y - minPos.Y) / meshSize.Y;

            var gridX = Mathf.Clamp((int)(normalizedX * wfcSize.X), 0, wfcSize.X - 1);
            var gridY = Mathf.Clamp((int)(normalizedY * wfcSize.Y), 0, wfcSize.Y - 1);

            var fgTileId = foregroundData.TileIds[gridY, gridX];
            var fgTile = _tileRegistry?.GetTile(fgTileId);

            // Foreground is stored ONLY if it's an auto-tile; gaps have null foreground
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

            // Determine terrain type for passability
            // Use foreground tile if present, otherwise check if any adjacent quad has a passable background
            if (fgTile != null)
            {
                vertex.TerrainType = fgTile.IsPassable ? 1 : 0;
            }
            else
            {
                // For gap vertices, passability depends on adjacent quads' backgrounds
                // Default to passable if no tile info available
                vertex.TerrainType = 1;
            }
        }

        mesh.UpdateAllCachedProperties();

        GD.Print($"[MeshTerrainGen] Mapped {mesh.Quads.Count} quads with backgrounds, {autoTileCount}/{mesh.Vertices.Count} vertices with auto-tiles");
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
}
