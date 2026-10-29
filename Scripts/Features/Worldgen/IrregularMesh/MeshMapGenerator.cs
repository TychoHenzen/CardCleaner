using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates the irregular map on the path the game runs: the mesh terrain from the seed cards
/// (<see cref="TerrainGeneratorFactory"/> and <see cref="IMeshTerrainGenerator.GenerateWithSampler"/>) over the
/// registered tile and biome registries, then the player start and the enemies from <see cref="MapPopulation"/>.
/// Runs off the main thread and returns an <see cref="IMeshGeneratedMap"/>.
/// </summary>
[Service(typeof(IMeshMapGenerator))]
public sealed class MeshMapGenerator : IMeshMapGenerator
{
    /// <summary>
    /// World units per mesh unit in the generated map data: the cell size <see cref="MapPopulation"/> measures in.
    /// </summary>
    public const float WorldScale = MapPopulation.CellWorldSize;

    /// <summary>
    /// The ring count of the generator the lazy [Service] scan builds: the map both game scenes play.
    /// </summary>
    public const int DefaultRings = 20;

    private const int RelaxationIterations = 8;

    private readonly ITileCatalog _tiles;
    private readonly BiomeSet _biomes;
    private readonly TransitionPairs _transitionPairs;
    private readonly IWfcTerrainSolver.IFactory _solverFactory;
    private readonly int _rings;

    /// <summary>
    /// The scanned constructor: every parameter is a registered service, so the container picks it.
    /// </summary>
    /// <param name="solverFactory">Builds the WFC terrain solver the mesh terrain solves with.</param>
    public MeshMapGenerator(
        ITileCatalog tiles,
        BiomeSet biomes,
        TransitionPairs transitionPairs,
        IWfcTerrainSolver.IFactory solverFactory)
        : this(tiles, biomes, transitionPairs, solverFactory, DefaultRings)
    {
    }

    /// <param name="solverFactory">Builds the WFC terrain solver the mesh terrain solves with.</param>
    /// <param name="rings">Rings of hexes around the mesh centre; more rings make a larger map.</param>
    internal MeshMapGenerator(
        ITileCatalog tiles,
        BiomeSet biomes,
        TransitionPairs transitionPairs,
        IWfcTerrainSolver.IFactory solverFactory,
        int rings)
    {
        _transitionPairs = transitionPairs ?? throw new ArgumentNullException(nameof(transitionPairs));
        _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
        _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
        _solverFactory = solverFactory ?? throw new ArgumentNullException(nameof(solverFactory));
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

        var terrain = TerrainGeneratorFactory.Create(_tiles, _biomes, _transitionPairs, _solverFactory);
        var sampler = TerrainGeneratorFactory.CardSampler(config.MapSeeds, seed);
        var mesh = terrain.GenerateWithSampler(rings, sampler, seed, RelaxationIterations);
        progress?.Report(0.9f);
        cancellationToken.ThrowIfCancellationRequested();

        var (mapData, placement, removeEnemy) =
            IrregularMaps.Populated(mesh, WorldScale, RandomSources.Create(config.Seed));

        ILog.Print(
            $"Irregular map generated: {rings} rings, {mapData.CellCount} cells, start cell " +
            $"{placement.PlayerStartCell}, {placement.EnemyCells.Count} enemies");
        progress?.Report(1f);
        return new IrregularGeneratedMap(mesh, mapData, WorldScale, removeEnemy);
    }
}
