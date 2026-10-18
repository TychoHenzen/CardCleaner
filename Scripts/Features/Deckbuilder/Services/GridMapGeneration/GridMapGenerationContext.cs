using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;

internal sealed class GridMapGenerationContext
{
    public required IRandom Rng { get; init; }
    public required IBiomeProvider BiomeProvider { get; init; }
    public required TileCatalogData Tiles { get; init; }
    public WfcMapGenerator? WfcGenerator { get; init; }
    public BiomeSet? Biomes { get; init; }
    public Func<Vector2I, Vector2I, CardSignature>? SignatureAt { get; init; }
    public IProfiler Profiler { get; set; } = new NoOpProfiler();
    public int MaxWfcRetries { get; set; } = 5;
    public required string DefaultPassableTileId { get; init; }
    public required string DefaultSolidTileId { get; init; }
}
