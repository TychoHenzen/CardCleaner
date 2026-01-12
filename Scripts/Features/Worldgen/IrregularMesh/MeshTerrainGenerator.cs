using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates irregular mesh terrain using WFC for tile assignment.
/// Combines mesh geometry generation with procedural terrain types.
/// </summary>
public class MeshTerrainGenerator
{
    private readonly Dictionary<string, HashSet<string>> _adjacencyRules;
    private readonly Dictionary<string, float> _tileWeights;
    private readonly Dictionary<string, int> _tileToTerrainType;

    /// <summary>
    /// Maximum WFC retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum WFC iterations per attempt.
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    /// <summary>
    /// Creates a terrain generator with the given tile rules.
    /// </summary>
    /// <param name="adjacencyRules">Which tiles can be adjacent to which.</param>
    /// <param name="tileToTerrainType">Mapping from tile ID to terrain type int.</param>
    /// <param name="tileWeights">Optional base weights for tile selection.</param>
    public MeshTerrainGenerator(
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType,
        Dictionary<string, float>? tileWeights = null)
    {
        _adjacencyRules = adjacencyRules;
        _tileToTerrainType = tileToTerrainType;
        _tileWeights = tileWeights ?? new Dictionary<string, float>();
    }

    /// <summary>
    /// Creates a permissive terrain generator where all tiles can be adjacent.
    /// </summary>
    public static MeshTerrainGenerator CreatePermissive(
        IEnumerable<string> tileIds,
        Dictionary<string, int> tileToTerrainType)
    {
        var tiles = tileIds.ToList();
        var rules = tiles.ToDictionary(t => t, _ => new HashSet<string>(tiles));
        return new MeshTerrainGenerator(rules, tileToTerrainType);
    }

    /// <summary>
    /// Generates an irregular mesh with WFC-assigned terrain.
    /// </summary>
    /// <param name="rings">Number of hex rings in the base grid.</param>
    /// <param name="biome">Optional biome for tile weighting.</param>
    /// <param name="seed">Random seed for generation.</param>
    /// <param name="relaxationIterations">Number of Lloyd relaxation iterations.</param>
    /// <returns>Mesh with terrain types assigned to vertices.</returns>
    public IrregularMesh Generate(
        int rings,
        BiomeDefinition? biome = null,
        int? seed = null,
        int relaxationIterations = 15)
    {
        // Generate mesh geometry
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);

        // Run WFC to assign terrain
        var wfcSeed = seed.HasValue ? (ulong)seed.Value : 0;
        AssignTerrain(mesh, biome, wfcSeed);

        return mesh;
    }

    /// <summary>
    /// Assigns terrain types to an existing mesh using WFC.
    /// </summary>
    public MeshWfcSolveResult AssignTerrain(
        IrregularMesh mesh,
        BiomeDefinition? biome = null,
        ulong seed = 0)
    {
        var solver = new MeshWfcSolver(_adjacencyRules, _tileWeights)
        {
            MaxIterations = MaxIterations
        };

        var (result, grid) = solver.SolveWithRetry(
            () => new MeshWfcGrid(mesh, _adjacencyRules.Keys),
            biome,
            seed,
            MaxRetries);

        if (result.Success)
        {
            grid.ApplyToMesh(_tileToTerrainType);
        }
        else
        {
            GD.PrintErr($"[MeshTerrainGenerator] WFC failed: {result.ErrorMessage}");
            // Apply fallback - use first tile type for all vertices
            var fallbackType = _tileToTerrainType.Values.FirstOrDefault();
            foreach (var vertex in mesh.Vertices)
            {
                vertex.TerrainType = fallbackType;
            }
            mesh.UpdateAllCachedProperties();
        }

        return result;
    }

    /// <summary>
    /// Generates terrain with signature-based biome selection.
    /// Uses the signature to influence tile weights.
    /// </summary>
    public IrregularMesh GenerateWithSignature(
        int rings,
        float[] signature,
        int? seed = null,
        int relaxationIterations = 15)
    {
        // Generate mesh geometry
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);

        // Create signature-based weights
        var signatureWeights = ComputeSignatureWeights(signature);

        var solver = new MeshWfcSolver(_adjacencyRules, signatureWeights)
        {
            MaxIterations = MaxIterations
        };

        var wfcSeed = seed.HasValue ? (ulong)seed.Value : 0;
        var (result, grid) = solver.SolveWithRetry(
            () => new MeshWfcGrid(mesh, _adjacencyRules.Keys),
            null,
            wfcSeed,
            MaxRetries);

        if (result.Success)
        {
            grid.ApplyToMesh(_tileToTerrainType);
        }
        else
        {
            GD.PrintErr($"[MeshTerrainGenerator] WFC with signature failed: {result.ErrorMessage}");
            var fallbackType = _tileToTerrainType.Values.FirstOrDefault();
            foreach (var vertex in mesh.Vertices)
            {
                vertex.TerrainType = fallbackType;
            }
            mesh.UpdateAllCachedProperties();
        }

        return mesh;
    }

    /// <summary>
    /// Generates terrain with card-based gradient and full constraint system.
    /// This is the preferred method for card-influenced map generation.
    /// </summary>
    /// <param name="rings">Number of hex rings in the base grid.</param>
    /// <param name="inputCards">The cards to use for gradient generation.</param>
    /// <param name="seed">Random seed for generation.</param>
    /// <param name="relaxationIterations">Number of Lloyd relaxation iterations.</param>
    /// <param name="useConstraints">Whether to enable spatial coherence and connectivity constraints.</param>
    /// <returns>Mesh with terrain types assigned to vertices.</returns>
    public IrregularMesh GenerateWithCards(
        int rings,
        CardSignature[] inputCards,
        int? seed = null,
        int relaxationIterations = 15,
        bool useConstraints = true)
    {
        // Generate mesh geometry
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = rings,
            Seed = seed,
            RelaxationIterations = relaxationIterations
        };
        var mesh = MeshGenerator.Generate(config);

        // Run WFC with card-based gradient
        var wfcSeed = seed.HasValue ? (ulong)seed.Value : 0;
        AssignTerrainWithCards(mesh, inputCards, wfcSeed, useConstraints);

        return mesh;
    }

    /// <summary>
    /// Assigns terrain to an existing mesh using card-based gradient and constraints.
    /// </summary>
    public MeshWfcSolveResult AssignTerrainWithCards(
        IrregularMesh mesh,
        CardSignature[] inputCards,
        ulong seed = 0,
        bool useConstraints = true)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = seed;

        // Calculate map bounds from mesh
        var bounds = mesh.CalculateBounds();

        // Create solver with base weights
        var solver = new MeshWfcSolver(_adjacencyRules, _tileWeights)
        {
            MaxIterations = MaxIterations
        };

        // Create grid factory for retry logic
        MeshWfcGrid CreateGrid() => new MeshWfcGrid(mesh, _adjacencyRules.Keys);

        // Setup constraints
        if (useConstraints)
        {
            // Add spatial coherence for natural-looking terrain clusters
            var coherenceConstraint = new MeshSpatialCoherenceConstraint
            {
                BoostFactor = 5.0f,
                TargetRegionSize = 30
            };
            solver.AddConstraint(coherenceConstraint);

            // Add connectivity to ensure passable regions are connected
            var passableTiles = GetPassableTiles();
            if (passableTiles.Count > 0)
            {
                var connectivityConstraint = new MeshConnectivityConstraint(passableTiles)
                {
                    DisconnectedPenalty = 0.0f // Hard constraint
                };
                solver.AddConstraint(connectivityConstraint);
            }

            // Add biome affinity if we have input cards
            if (inputCards.Length > 0)
            {
                var gradient = new MeshCardBasedGradient(inputCards, bounds, rng);
                var grid = CreateGrid();
                var biomeConstraint = new MeshBiomeAffinityConstraint(gradient, grid)
                {
                    BoostFactor = 3.0f
                };
                biomeConstraint.SetDefaultAffinities(_adjacencyRules.Keys);
                solver.AddConstraint(biomeConstraint);
            }
        }

        // Solve WFC
        var (result, finalGrid) = solver.SolveWithRetry(
            CreateGrid,
            null, // Biome handled by constraint
            seed,
            MaxRetries);

        if (result.Success)
        {
            finalGrid.ApplyToMesh(_tileToTerrainType);
        }
        else
        {
            GD.PrintErr($"[MeshTerrainGenerator] WFC with cards failed: {result.ErrorMessage}");
            var fallbackType = _tileToTerrainType.Values.FirstOrDefault();
            foreach (var vertex in mesh.Vertices)
            {
                vertex.TerrainType = fallbackType;
            }
            mesh.UpdateAllCachedProperties();
        }

        return result;
    }

    /// <summary>
    /// Gets tiles that are considered passable based on naming conventions.
    /// </summary>
    private HashSet<string> GetPassableTiles()
    {
        var passable = new HashSet<string>();
        foreach (var tileId in _adjacencyRules.Keys)
        {
            var lowerTile = tileId.ToLowerInvariant();
            // Tiles with these names are typically passable
            if (lowerTile.Contains("grass") ||
                lowerTile.Contains("dirt") ||
                lowerTile.Contains("sand") ||
                lowerTile.Contains("path") ||
                lowerTile.Contains("floor"))
            {
                passable.Add(tileId);
            }
            // Tiles with these names are typically impassable
            else if (!lowerTile.Contains("rock") &&
                     !lowerTile.Contains("wall") &&
                     !lowerTile.Contains("water") &&
                     !lowerTile.Contains("lava"))
            {
                // Default to passable if not obviously blocked
                passable.Add(tileId);
            }
        }
        return passable;
    }

    /// <summary>
    /// Computes tile weights based on a signature.
    /// Signature dimensions affect terrain preferences.
    /// </summary>
    private Dictionary<string, float> ComputeSignatureWeights(float[] signature)
    {
        var weights = new Dictionary<string, float>(_tileWeights);

        // Use signature dimensions to influence weights
        // These are heuristics based on the card signature system:
        // [0] Solidum (solid vs air) - affects ground vs gap tiles
        // [1] Febris (cold vs hot) - affects water vs fire tiles
        // [2] Ordinem (chaos vs order) - affects variation
        // [3] Lumines (dark vs light) - affects dark vs light terrain
        // [4] Varias (time vs space) - affects variation
        // [5] Inertiae (heavy vs light) - affects rock vs grass
        // [6] Subsidium (harmful vs helpful) - affects hazards
        // [7] Spatium (near vs far) - affects density

        if (signature.Length >= 2)
        {
            // Temperature affects water/ice vs sand/fire
            var temperature = signature[1];
            foreach (var tile in weights.Keys.ToList())
            {
                var lowerTile = tile.ToLowerInvariant();
                if (lowerTile.Contains("water") || lowerTile.Contains("ice"))
                {
                    weights[tile] *= 1f + (-temperature); // Cold = more water
                }
                else if (lowerTile.Contains("sand") || lowerTile.Contains("fire") || lowerTile.Contains("lava"))
                {
                    weights[tile] *= 1f + temperature; // Hot = more sand/fire
                }
            }
        }

        if (signature.Length >= 6)
        {
            // Density affects rock vs grass
            var density = signature[5];
            foreach (var tile in weights.Keys.ToList())
            {
                var lowerTile = tile.ToLowerInvariant();
                if (lowerTile.Contains("rock") || lowerTile.Contains("stone"))
                {
                    weights[tile] *= 1f + density; // Dense = more rock
                }
                else if (lowerTile.Contains("grass") || lowerTile.Contains("plant"))
                {
                    weights[tile] *= 1f + (-density); // Light = more grass
                }
            }
        }

        // Ensure all weights are positive
        foreach (var tile in weights.Keys.ToList())
        {
            weights[tile] = Mathf.Max(0.1f, weights[tile]);
        }

        return weights;
    }
}
