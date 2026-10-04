using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Creates a connected map using WFC for terrain generation with biome-based tile selection.
/// Uses Wave Function Collapse for hard constraint satisfaction (2x2 window, adjacency rules)
/// then applies post-processing for structures, variants, and connectivity.
/// </summary>
public class SimpleMapGenerator
{
    private readonly SimpleMapGenerationPipeline _pipeline;

    /// <summary>
    /// Maximum WFC retry attempts on contradiction (default 5).
    /// </summary>
    public int MaxWfcRetries
    {
        get => _pipeline.MaxWfcRetries;
        set => _pipeline.MaxWfcRetries = value;
    }

    public SimpleMapGenerator(
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

        var context = new SimpleMapGenerationContext
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
        };
        _pipeline = new SimpleMapGenerationPipeline(context);
    }

    public void SetProfiler(IProfiler profiler)
    {
        _pipeline.SetProfiler(profiler);
    }

    public SimpleMapData GenerateMap(Vector2I size)
    {
        return _pipeline.GenerateMap(size);
    }
}
