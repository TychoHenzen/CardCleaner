using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// A map from the default generator together with the biome provider that shaped it.
/// </summary>
internal sealed record DefaultMapResult(SimpleMapData Map, BiomeMapGenerator BiomeProvider);
