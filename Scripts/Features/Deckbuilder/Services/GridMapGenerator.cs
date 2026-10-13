using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Generates the grid map: biomes from a gradient over the seed cards, WFC terrain with hard constraints (2x2 window,
/// adjacency rules), then variants, the player start and the enemies. Runs off the main thread, reports progress at
/// the profiled phase boundaries and stops at the next boundary once the token is cancelled.
/// </summary>
public sealed class GridMapGenerator : IGridMapGenerator
{
    private readonly ITileRegistry _tileRegistry;
    private readonly ITileMetadataProvider _metadataProvider;
    private readonly BiomeRegistry _biomeRegistry;

    public GridMapGenerator(
        ITileRegistry tileRegistry,
        ITileMetadataProvider metadataProvider,
        BiomeRegistry biomeRegistry)
    {
        _tileRegistry = tileRegistry ?? throw new ArgumentNullException(nameof(tileRegistry));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
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

        // Everything is built per call from the seed: one RNG drawn in a fixed order (gradient, then terrain,
        // variants, start and enemies), so the same seed and cards always give the same map.
        var rng = new RandomNumberGenerator { Seed = config.Seed };
        var gradient = new CardBasedGradient(config.MapSeeds, rng);
        var biomeProvider = new BiomeMapGenerator(_biomeRegistry, gradient, config.Size);
        var transitionPairs = new CompiledTransitionResolver().GetAllTransitionPairs().ToList();
        var wfcGenerator = new WfcMapGenerator(
            new WfcAdjacencyRules(transitionPairs),
            new TileRegistryWfcCatalog(_tileRegistry));
        var pipeline = CreatePipeline(
            rng, biomeProvider, _tileRegistry, _metadataProvider, wfcGenerator, _biomeRegistry, gradient);
        pipeline.SetProfiler(new CancellableProgressProfiler(progress, cancellationToken));

        SimpleMapData map;
        try
        {
            map = await Task.Run(() => pipeline.GenerateMap(config.Size), cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            throw new MapGenerationCancelledException("Map generation was cancelled by user", ex);
        }

        biomeProvider.LogBiomeStats();
        ILog.Print($"Grid map generated: {config.Size.X}x{config.Size.Y}, {map.EnemyPositions.Count} enemies");
        return new SimpleGeneratedMap(map);
    }

    /// <summary>
    /// Builds the generation pipeline over one RNG. Tests use it to run the pipeline with their own biome provider,
    /// WFC generator or gradient.
    /// </summary>
    internal static SimpleMapGenerationPipeline CreatePipeline(
        RandomNumberGenerator rng,
        IBiomeProvider biomeProvider,
        ITileRegistry tileRegistry,
        ITileMetadataProvider metadataProvider,
        WfcMapGenerator? wfcGenerator = null,
        BiomeRegistry? biomeRegistry = null,
        BaselineGradient? gradient = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        return new SimpleMapGenerationPipeline(new SimpleMapGenerationContext
        {
            Rng = rng,
            BiomeProvider = biomeProvider,
            TileRegistry = tileRegistry,
            MetadataProvider = metadataProvider,
            WfcGenerator = wfcGenerator,
            BiomeRegistry = biomeRegistry,
            Gradient = gradient,
            DefaultPassableTileId = metadataProvider.GetDefaultPassableTileId() ?? "floor",
            DefaultSolidTileId = metadataProvider.GetDefaultSolidTileId() ?? "wall"
        });
    }
}
