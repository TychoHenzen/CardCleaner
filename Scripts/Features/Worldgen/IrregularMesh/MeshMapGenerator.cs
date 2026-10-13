using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Generates the irregular map on the path the game runs: the mesh terrain from the seed cards
/// (<see cref="TerrainGeneratorFactory"/> and <see cref="MeshTerrainGenerator.GenerateWithCards"/>) over the
/// registered tile and biome registries, then the player start and the enemies from <see cref="MapPopulation"/>.
/// Runs off the main thread and returns an <see cref="IMeshGeneratedMap"/>.
/// </summary>
public sealed class MeshMapGenerator : IMeshMapGenerator
{
    /// <summary>World units per mesh unit in the generated map data; enemy distances are measured in this scale.</summary>
    public const float WorldScale = 16f;

    /// <summary>The ring count of a plain <see cref="MapGenerationConfig"/>: the value both game scenes use.</summary>
    public const int DefaultRings = 20;

    private const int RelaxationIterations = 8;

    private readonly ITileRegistry _tileRegistry;
    private readonly BiomeRegistry _biomeRegistry;

    public MeshMapGenerator(ITileRegistry tileRegistry, BiomeRegistry biomeRegistry)
    {
        _tileRegistry = tileRegistry ?? throw new ArgumentNullException(nameof(tileRegistry));
        _biomeRegistry = biomeRegistry ?? throw new ArgumentNullException(nameof(biomeRegistry));
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

        // ASSUMPTION: until the session hands this generator its own config (#285), the ring count comes from a
        // MeshMapGenerationConfig (the screen's MeshRings); a plain config gets DefaultRings, the scenes' value.
        var rings = config is MeshMapGenerationConfig meshConfig ? meshConfig.Rings : DefaultRings;
        // The screen's int seed travels as a ulong; the unchecked casts give the same int back.
        var seed = unchecked((int)config.Seed);

        var terrain = TerrainGeneratorFactory.Create(_tileRegistry, _biomeRegistry);
        var mesh = terrain.GenerateWithCards(rings, config.MapSeeds, seed, RelaxationIterations);
        progress?.Report(0.9f);
        cancellationToken.ThrowIfCancellationRequested();

        var mapData = new IrregularMeshMapData(mesh) { WorldScale = WorldScale };
        var placement = MapPopulation.Place(mapData, new RandomNumberGenerator { Seed = config.Seed });
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
