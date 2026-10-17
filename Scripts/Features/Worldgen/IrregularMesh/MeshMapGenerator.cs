using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates the irregular map on the path the game runs: the mesh terrain from the seed cards
/// (<see cref="TerrainGeneratorFactory"/> and <see cref="MeshTerrainGenerator.GenerateWithSampler"/>) over the
/// registered tile and biome registries, then the player start and the enemies from <see cref="MapPopulation"/>.
/// Runs off the main thread and returns an <see cref="IMeshGeneratedMap"/>.
/// </summary>
public sealed class MeshMapGenerator : IMeshMapGenerator
{
    /// <summary>
    /// World units per mesh unit in the generated map data: the cell size <see cref="MapPopulation"/> measures in.
    /// </summary>
    public const float WorldScale = MapPopulation.CellWorldSize;

    /// <summary>
    /// The ring count of the generator the composition root registers: the map both game scenes play.
    /// </summary>
    public const int DefaultRings = 20;

    private const int RelaxationIterations = 8;

    private readonly ITileRegistry _tileRegistry;
    private readonly BiomeRegistry _biomeRegistry;
    private readonly TransitionPairs _transitionPairs;
    private readonly int _rings;

    /// <param name="rings">Rings of hexes around the mesh centre; more rings make a larger map.</param>
    public MeshMapGenerator(
        ITileRegistry tileRegistry,
        BiomeRegistry biomeRegistry,
        TransitionPairs transitionPairs,
        int rings = DefaultRings)
    {
        _transitionPairs = transitionPairs ?? throw new ArgumentNullException(nameof(transitionPairs));
        _tileRegistry = tileRegistry ?? throw new ArgumentNullException(nameof(tileRegistry));
        _biomeRegistry = biomeRegistry ?? throw new ArgumentNullException(nameof(biomeRegistry));
        ArgumentOutOfRangeException.ThrowIfLessThan(rings, 1);
        _rings = rings;
    }

    /// <inheritdoc />
    /// <exception cref="MapGenerationCancelledException">Thrown when the token is cancelled.</exception>
    public async Task<IGeneratedMap> GenerateAsync(
        MapGenerationConfig config,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            return await Task.Run(() => Generate(config, progress, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            throw new MapGenerationCancelledException("Map generation was cancelled by user", ex);
        }
    }

    private IGeneratedMap Generate(
        MapGenerationConfig config,
        IProgress<float>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rings = _rings;
        // The terrain generator takes an int seed; the session's ulong seed is cut to its low 32 bits.
        var seed = unchecked((int)config.Seed);

        var terrain = TerrainGeneratorFactory.Create(_tileRegistry, _biomeRegistry, _transitionPairs);
        var sampler = TerrainGeneratorFactory.CardSampler(config.MapSeeds, seed);
        var mesh = terrain.GenerateWithSampler(rings, sampler, seed, RelaxationIterations);
        progress?.Report(0.9f);
        cancellationToken.ThrowIfCancellationRequested();

        var mapData = new IrregularMeshMapData(mesh) { WorldScale = WorldScale };
        var placement = MapPopulation.Place(mapData, RandomSources.Create(config.Seed));
        mapData.SetPlayerStart(placement.PlayerStartCell);
        foreach (var enemyCell in placement.EnemyCells)
            mapData.AddEnemySpawn(enemyCell);

        ILog.Print(
            $"Irregular map generated: {rings} rings, {mapData.CellCount} cells, start cell " +
            $"{placement.PlayerStartCell}, {placement.EnemyCells.Count} enemies");
        progress?.Report(1f);
        return new IrregularGeneratedMap(mesh, mapData);
    }
}
