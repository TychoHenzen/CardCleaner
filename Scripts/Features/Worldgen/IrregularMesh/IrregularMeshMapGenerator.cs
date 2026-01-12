using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Map generator that creates irregular mesh maps.
/// Implements IMapGenerator for integration with GameSessionService.
/// </summary>
public class IrregularMeshMapGenerator : IMapGenerator
{
    private readonly Dictionary<string, HashSet<string>>? _adjacencyRules;
    private readonly Dictionary<string, int>? _tileToTerrainType;

    /// <summary>
    /// World scale for mesh coordinates.
    /// </summary>
    public float WorldScale { get; set; } = 16f;

    /// <summary>
    /// Number of enemies to spawn (can be calculated from map size).
    /// </summary>
    public int EnemyCount { get; set; } = 3;

    /// <summary>
    /// Minimum distance between enemy spawns (in cells).
    /// </summary>
    public int MinEnemyDistance { get; set; } = 5;

    /// <summary>
    /// Creates a generator with default terrain assignment.
    /// </summary>
    public IrregularMeshMapGenerator()
    {
    }

    /// <summary>
    /// Creates a generator with WFC terrain assignment.
    /// </summary>
    /// <param name="adjacencyRules">WFC adjacency rules.</param>
    /// <param name="tileToTerrainType">Mapping from tile ID to terrain type.</param>
    public IrregularMeshMapGenerator(
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType)
    {
        _adjacencyRules = adjacencyRules;
        _tileToTerrainType = tileToTerrainType;
    }

    /// <inheritdoc />
    public Task<IGeneratedMap> GenerateAsync(
        MapGenerationConfig config,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Run generation on background thread
        return Task.Run(() => Generate(config, progress, cancellationToken), cancellationToken);
    }

    private IGeneratedMap Generate(
        MapGenerationConfig config,
        IProgress<float>? progress,
        CancellationToken cancellationToken)
    {
        var rng = new RandomNumberGenerator();
        rng.Seed = config.Seed;

        progress?.Report(0.1f);
        cancellationToken.ThrowIfCancellationRequested();

        // Calculate mesh rings based on size hint
        // Approximate: each ring adds ~6 quads, we want roughly size.X * size.Y / 100 quads
        var targetQuads = config.Size.X * config.Size.Y / 100;
        var rings = Math.Max(3, (int)Math.Sqrt(targetQuads / 6.0) + 2);

        GD.Print($"[IrregularMeshMapGenerator] Generating mesh with {rings} rings for target size {config.Size}");

        // Generate mesh geometry
        IrregularMesh mesh;
        if (_adjacencyRules != null && _tileToTerrainType != null)
        {
            // Use WFC terrain generation
            progress?.Report(0.2f);
            var terrainGen = new MeshTerrainGenerator(_adjacencyRules, _tileToTerrainType);

            // Use card-based gradient if cards are provided
            if (config.MapSeeds.Length > 0)
            {
                GD.Print($"[IrregularMeshMapGenerator] Using card-based gradient with {config.MapSeeds.Length} cards");
                mesh = terrainGen.GenerateWithCards(rings, config.MapSeeds, (int)config.Seed);
            }
            else
            {
                mesh = terrainGen.Generate(rings, config.BiomeRegistry?.GetBiome("plains"), (int)config.Seed);
            }
        }
        else
        {
            // Use default terrain generation
            progress?.Report(0.2f);
            var meshConfig = new MeshGenerator.GenerationConfig
            {
                Rings = rings,
                Seed = (int)config.Seed,
                RelaxationIterations = 15
            };
            mesh = MeshGenerator.Generate(meshConfig);

            // Assign default terrain (70% passable, 30% impassable)
            progress?.Report(0.4f);
            AssignDefaultTerrain(mesh, rng);
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.6f);

        // Create map data adapter
        var mapData = new IrregularMeshMapData(mesh)
        {
            WorldScale = WorldScale,
            PassableTerrainType = 1 // Terrain type 1 is passable
        };

        // Find passable cells for spawn placement
        var passableCells = new List<int>();
        for (int i = 0; i < mapData.CellCount; i++)
        {
            if (mapData.IsPassable(i))
                passableCells.Add(i);
        }

        if (passableCells.Count == 0)
        {
            GD.PrintErr("[IrregularMeshMapGenerator] No passable cells found!");
            // Make at least some cells passable as fallback
            for (int i = 0; i < Math.Min(10, mesh.Vertices.Count); i++)
            {
                mesh.Vertices[i].TerrainType = 1;
            }
            mesh.UpdateAllCachedProperties();

            passableCells.Clear();
            for (int i = 0; i < mapData.CellCount; i++)
            {
                if (mapData.IsPassable(i))
                    passableCells.Add(i);
            }
        }

        progress?.Report(0.7f);
        cancellationToken.ThrowIfCancellationRequested();

        // Place player start
        var playerStartIdx = rng.RandiRange(0, passableCells.Count - 1);
        var playerStartCell = passableCells[playerStartIdx];
        mapData.SetPlayerStart(playerStartCell);

        GD.Print($"[IrregularMeshMapGenerator] Player start at cell {playerStartCell}");

        progress?.Report(0.8f);

        // Place enemies
        PlaceEnemies(mapData, passableCells, playerStartCell, rng);

        progress?.Report(0.9f);
        cancellationToken.ThrowIfCancellationRequested();

        GD.Print($"[IrregularMeshMapGenerator] Generated map with {mesh.Vertices.Count} vertices, " +
                 $"{mesh.Quads.Count} quads, {passableCells.Count} passable cells, " +
                 $"{mapData.EnemySpawnCells.Count} enemies");

        progress?.Report(1.0f);

        return new IrregularGeneratedMap(mesh, mapData);
    }

    private void AssignDefaultTerrain(IrregularMesh mesh, RandomNumberGenerator rng)
    {
        foreach (var vertex in mesh.Vertices)
        {
            // 70% passable (terrain type 1), 30% impassable (terrain type 0)
            vertex.TerrainType = rng.Randf() < 0.7f ? 1 : 0;
        }
        mesh.UpdateAllCachedProperties();
    }

    private void PlaceEnemies(
        IrregularMeshMapData mapData,
        List<int> passableCells,
        int playerStartCell,
        RandomNumberGenerator rng)
    {
        // Calculate actual enemy count based on passable area
        var actualEnemyCount = Math.Min(EnemyCount, passableCells.Count / 20);
        actualEnemyCount = Math.Max(1, actualEnemyCount);

        var placedEnemies = new List<int>();
        var playerPos = mapData.GetCellCenter(playerStartCell);
        var minDistSq = MinEnemyDistance * MinEnemyDistance * mapData.WorldScale * mapData.WorldScale;

        // Try to place enemies with spacing
        var attempts = 0;
        var maxAttempts = passableCells.Count * 2;

        while (placedEnemies.Count < actualEnemyCount && attempts < maxAttempts)
        {
            attempts++;

            var candidateIdx = rng.RandiRange(0, passableCells.Count - 1);
            var candidateCell = passableCells[candidateIdx];

            // Skip if too close to player start
            var candidatePos = mapData.GetCellCenter(candidateCell);
            if (candidatePos.DistanceSquaredTo(playerPos) < minDistSq)
                continue;

            // Skip if too close to other enemies
            var tooClose = false;
            foreach (var existingEnemy in placedEnemies)
            {
                var existingPos = mapData.GetCellCenter(existingEnemy);
                if (candidatePos.DistanceSquaredTo(existingPos) < minDistSq)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            // Place enemy
            mapData.AddEnemySpawn(candidateCell);
            placedEnemies.Add(candidateCell);
        }

        GD.Print($"[IrregularMeshMapGenerator] Placed {placedEnemies.Count}/{actualEnemyCount} enemies");
    }
}
