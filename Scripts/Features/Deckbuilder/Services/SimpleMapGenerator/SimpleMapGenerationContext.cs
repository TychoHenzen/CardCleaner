using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.SimpleMapGeneratorSupport;

internal sealed class SimpleMapGenerationContext
{
    public required RandomNumberGenerator Rng { get; init; }
    public required IBiomeProvider BiomeProvider { get; init; }
    public required ITileRegistry TileRegistry { get; init; }
    public required ITileMetadataProvider MetadataProvider { get; init; }
    public WfcMapGenerator? WfcGenerator { get; init; }
    public BiomeRegistry? BiomeRegistry { get; init; }
    public BaselineGradient? Gradient { get; init; }
    public IProfiler Profiler { get; set; } = new NoOpProfiler();
    public int MaxWfcRetries { get; set; } = 5;
    public required string DefaultPassableTileId { get; init; }
    public required string DefaultSolidTileId { get; init; }
}
