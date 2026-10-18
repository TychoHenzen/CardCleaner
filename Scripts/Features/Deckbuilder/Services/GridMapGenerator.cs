using System;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Generates the grid map: variants, biomes from a gradient over the seed cards, WFC terrain with hard constraints
/// (2x2 window, adjacency rules), then the player start and the enemies. Runs off the main thread, reports progress
/// at the pipeline's phase boundaries (reaching 1.0 when done). Once the token is cancelled it stops at the next
/// boundary, and a running WFC solve stops within one collapsed cell.
/// </summary>
public sealed class GridMapGenerator : IGridMapGenerator
{
    private readonly ITileRegistry _tileRegistry;
    private readonly ITileMetadataProvider _metadataProvider;
    private readonly BiomeSet _biomes;
    private readonly TransitionPairs _transitionPairs;

    public GridMapGenerator(
        ITileRegistry tileRegistry,
        ITileMetadataProvider metadataProvider,
        BiomeSet biomes,
        TransitionPairs transitionPairs)
    {
        _tileRegistry = tileRegistry ?? throw new ArgumentNullException(nameof(tileRegistry));
        _metadataProvider = metadataProvider ?? throw new ArgumentNullException(nameof(metadataProvider));
        _biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
        _transitionPairs = transitionPairs ?? throw new ArgumentNullException(nameof(transitionPairs));
    }

    /// <inheritdoc />
    /// <exception cref="MapGenerationCancelledException">Thrown when the token is cancelled.</exception>
    public async Task<IGeneratedMap> GenerateAsync(
        MapGenerationConfig config,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        // Everything is built per call from the seed. One RNG is drawn in the order GridMapGenerationPipeline's
        // GenerateMap fixes (variants, the gradient while the biome map is built, terrain, the start, the enemies),
        // so the same seed and cards always give the same map.
        var rng = RandomSources.Create(config.Seed);
        var gradient = Gradients.FromCards(config.MapSeeds, rng);
        var biomeProvider = BiomeMaps.Create(
            _biomes,
            gradient.GetSignatureAt,
            config.Size,
            _metadataProvider.GetDefaultPassableTileId(),
            _metadataProvider.GetDefaultSolidTileId());
        var wfcGenerator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs.Pairs),
            new TileRegistryWfcCatalog(_tileRegistry));
        var pipeline = CreatePipeline(
            rng, biomeProvider, _tileRegistry, _metadataProvider, wfcGenerator, _biomes, gradient);
        pipeline.SetProfiler(new CancellableProgressProfiler(
            progress, GridMapGenerationPipeline.ProgressWeights, cancellationToken));

        SimpleMapData map;
        try
        {
            map = await Task.Run(() => pipeline.GenerateMap(config.Size, cancellationToken), cancellationToken);
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
    internal static GridMapGenerationPipeline CreatePipeline(
        IRandom rng,
        IBiomeProvider biomeProvider,
        ITileRegistry tileRegistry,
        ITileMetadataProvider metadataProvider,
        WfcMapGenerator? wfcGenerator = null,
        BiomeSet? biomes = null,
        BaselineGradient? gradient = null)
    {
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(biomeProvider);
        ArgumentNullException.ThrowIfNull(tileRegistry);
        ArgumentNullException.ThrowIfNull(metadataProvider);

        Func<Vector2I, Vector2I, CardSignature>? signatureAt = null;
        if (gradient != null)
            signatureAt = gradient.GetSignatureAt;

        return new GridMapGenerationPipeline(new GridMapGenerationContext
        {
            Rng = rng,
            BiomeProvider = biomeProvider,
            TileRegistry = tileRegistry,
            MetadataProvider = metadataProvider,
            WfcGenerator = wfcGenerator,
            Biomes = biomes,
            SignatureAt = signatureAt,
            DefaultPassableTileId = metadataProvider.GetDefaultPassableTileId() ?? "floor",
            DefaultSolidTileId = metadataProvider.GetDefaultSolidTileId() ?? "wall"
        });
    }
}
