using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Configuration for creating a unified WFC solver.
/// </summary>
public class UnifiedWfcConfig
{
    /// <summary>
    /// Adjacency rules defining which tiles can be neighbors.
    /// </summary>
    public IAdjacencyRules? AdjacencyRules { get; set; }

    /// <summary>
    /// Whether to use 8-way propagation (important for dual-grid rendering).
    /// Default true for regular grids, false for meshes.
    /// </summary>
    public bool Use8WayPropagation { get; set; } = true;

    /// <summary>
    /// Maximum iterations before giving up.
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>
    /// Weight multiplier for tiles matching collapsed neighbors.
    /// </summary>
    public float ContinuityBiasMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Tile registry for constraint evaluation.
    /// </summary>
    public ITileRegistry? TileRegistry { get; set; }

    /// <summary>
    /// Whether to add the auto-tile gap constraint.
    /// </summary>
    public bool EnableAutoTileGapConstraint { get; set; } = true;

    /// <summary>
    /// Whether to add the spatial coherence constraint.
    /// </summary>
    public bool EnableSpatialCoherence { get; set; } = true;

    /// <summary>
    /// Target region size for spatial coherence.
    /// </summary>
    public int SpatialCoherenceTargetSize { get; set; } = 50;

    /// <summary>
    /// Boost factor for spatial coherence.
    /// </summary>
    public float SpatialCoherenceBoost { get; set; } = 10.0f;

    /// <summary>
    /// Additional custom constraints to add.
    /// </summary>
    public List<IUnifiedWfcConstraint> AdditionalConstraints { get; } = new();
}

/// <summary>
/// Factory for creating unified WFC solvers with standard constraint configurations.
/// </summary>
public static class UnifiedWfcFactory
{
    /// <summary>
    /// Creates a unified WFC solver with the specified configuration.
    /// </summary>
    public static UnifiedWfcSolver CreateSolver(UnifiedWfcConfig config)
    {
        var rules = config.AdjacencyRules ?? throw new ArgumentException("AdjacencyRules is required");

        var propagator = new UnifiedWfcPropagator(rules, config.Use8WayPropagation);
        var selector = new UnifiedWfcTileSelector
        {
            ContinuityBiasMultiplier = config.ContinuityBiasMultiplier
        };

        var solver = new UnifiedWfcSolver(propagator, selector)
        {
            MaxIterations = config.MaxIterations,
            ContinuityBiasMultiplier = config.ContinuityBiasMultiplier
        };

        // Add standard constraints
        if (config.EnableAutoTileGapConstraint && config.TileRegistry != null)
        {
            solver.AddConstraint(new UnifiedAutoTileGapConstraint(config.TileRegistry));
        }

        if (config.EnableSpatialCoherence)
        {
            solver.AddConstraint(new UnifiedSpatialCoherenceConstraint
            {
                TargetRegionSize = config.SpatialCoherenceTargetSize,
                BoostFactor = config.SpatialCoherenceBoost
            });
        }

        // Add any additional custom constraints
        foreach (var constraint in config.AdditionalConstraints)
        {
            solver.AddConstraint(constraint);
        }

        return solver;
    }

    /// <summary>
    /// Creates a solver configured for regular 2D grids (dual-grid rendering).
    /// </summary>
    public static UnifiedWfcSolver CreateForRegularGrid(
        WfcAdjacencyRules rules,
        ITileRegistry? tileRegistry = null)
    {
        return CreateSolver(new UnifiedWfcConfig
        {
            AdjacencyRules = new AdjacencyRulesAdapter(rules),
            Use8WayPropagation = true, // Important for dual-grid
            TileRegistry = tileRegistry,
            EnableAutoTileGapConstraint = tileRegistry != null,
            EnableSpatialCoherence = true
        });
    }

    /// <summary>
    /// Creates a solver configured for irregular meshes.
    /// </summary>
    public static UnifiedWfcSolver CreateForMesh(
        IEnumerable<string> tileIds,
        ITileRegistry? tileRegistry = null)
    {
        return CreateSolver(new UnifiedWfcConfig
        {
            AdjacencyRules = new PermissiveAdjacencyRules(tileIds),
            Use8WayPropagation = false, // Meshes don't have 8-way neighbors
            TileRegistry = tileRegistry,
            EnableAutoTileGapConstraint = tileRegistry != null,
            EnableSpatialCoherence = true
        });
    }

    /// <summary>
    /// Creates a solver for permissive generation (any tile can neighbor any other).
    /// </summary>
    public static UnifiedWfcSolver CreatePermissive(
        IEnumerable<string> tileIds,
        bool use8Way = false)
    {
        return CreateSolver(new UnifiedWfcConfig
        {
            AdjacencyRules = new PermissiveAdjacencyRules(tileIds),
            Use8WayPropagation = use8Way,
            EnableAutoTileGapConstraint = false,
            EnableSpatialCoherence = true
        });
    }
}

/// <summary>
/// Result of a unified map generation operation.
/// </summary>
public readonly struct UnifiedMapResult
{
    public bool Success { get; }
    public int Iterations { get; }
    public string? ErrorMessage { get; }
    public int? ContradictionCellId { get; }

    private UnifiedMapResult(bool success, int iterations, string? error, int? cellId)
    {
        Success = success;
        Iterations = iterations;
        ErrorMessage = error;
        ContradictionCellId = cellId;
    }

    public static UnifiedMapResult Succeeded(int iterations) => new(true, iterations, null, null);
    public static UnifiedMapResult Failed(string error, int iterations, int? cellId = null) =>
        new(false, iterations, error, cellId);
}

/// <summary>
/// Helper class for running unified WFC on different grid types.
/// </summary>
public static class UnifiedWfcRunner
{
    /// <summary>
    /// Runs WFC on a regular 2D grid and returns the collapsed tile IDs.
    /// </summary>
    public static (UnifiedMapResult result, string[,]? tileMap) RunOnGrid(
        UnifiedWfcSolver solver,
        int width,
        int height,
        IEnumerable<string> tileIds,
        BiomeDefinition? biome,
        ulong seed,
        int maxRetries = 3)
    {
        var (result, grid) = solver.SolveWithRetry(
            () => new WfcGrid(width, height, tileIds),
            biome,
            seed,
            maxRetries);

        if (!result.Success || grid is not WfcGrid wfcGrid)
        {
            return (UnifiedMapResult.Failed(
                result.ErrorMessage ?? "Unknown error",
                result.Iterations,
                result.ContradictionCellId), null);
        }

        // Extract tile map from solved grid
        var tileMap = new string[height, width];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var cell = wfcGrid.GetCell(x, y);
                tileMap[y, x] = cell.IsCollapsed() ? cell.GetCollapsedTile() : "";
            }
        }

        return (UnifiedMapResult.Succeeded(result.Iterations), tileMap);
    }

    /// <summary>
    /// Runs WFC on an irregular mesh grid and updates the mesh vertices.
    /// </summary>
    public static UnifiedMapResult RunOnMesh(
        UnifiedWfcSolver solver,
        CardCleaner.Scripts.Features.Worldgen.IrregularMesh.MeshWfcGrid meshGrid,
        BiomeDefinition? biome,
        ulong seed,
        Dictionary<string, int>? tileToTerrainType = null,
        int maxRetries = 3)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = seed;

        var result = solver.Solve(meshGrid, biome, rng);

        if (!result.Success)
        {
            // Try retries
            for (var attempt = 1; attempt <= maxRetries && !result.Success; attempt++)
            {
                rng.Seed = seed + (ulong)attempt;

                // Reset all constraints
                foreach (var constraint in solver.GetConstraints())
                {
                    constraint.Reset();
                }

                // Note: We can't easily recreate the mesh grid, so we just retry with different seed
                // A more robust solution would clone the grid state
                result = solver.Solve(meshGrid, biome, rng);
            }
        }

        if (result.Success && tileToTerrainType != null)
        {
            // Apply results to mesh
            meshGrid.ApplyToMesh(tileToTerrainType);
        }

        return result.Success
            ? UnifiedMapResult.Succeeded(result.Iterations)
            : UnifiedMapResult.Failed(
                result.ErrorMessage ?? "Unknown error",
                result.Iterations,
                result.ContradictionCellId);
    }
}
