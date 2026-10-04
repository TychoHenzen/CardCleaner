using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;

/// <summary>
/// Builds a map with the default WFC-based SimpleMapGenerator on a background thread.
/// </summary>
internal sealed class DefaultSessionMapBuilder
{
    private readonly SessionServices _services;

    internal DefaultSessionMapBuilder(SessionServices services)
    {
        _services = services;
    }

    internal async Task<DefaultMapResult> BuildAsync(MapGenerationRequest request)
    {
        // Ensure tile registry is available (fallback if async callback hasn't run yet)
        var tileRegistry = _services.TileRegistry ??= new TileRegistry();

        // Create gradient from all map seeds for biome placement
        var gradient = new CardBasedGradient(request.MapSeeds, request.Rng);

        // Create biome provider that maps gradient signatures to biomes
        var biomeProvider = new BiomeMapGenerator(_services.BiomeRegistry, gradient, request.MapSize);

        // Create WFC generator with hard constraints (2x2 window, adjacency rules)
        // Pass tile registry so WfcMapGenerator uses TileDefinition.IsPassable for connectivity
        var transitionResolver = new CompiledTransitionResolver();
        var wfcGenerator = new WfcMapGenerator(transitionResolver, tileRegistry);

        // Create map generator with WFC for terrain generation
        var mapGenerator = new SimpleMapGenerator(
            request.Rng,
            biomeProvider,
            tileRegistry,
            _services.MetadataProvider,
            wfcGenerator,
            _services.BiomeRegistry,
            gradient);

        // Wrap in async adapter and generate on background thread
        var asyncGenerator = new AsyncMapGeneratorAdapter(mapGenerator);
        var map = await asyncGenerator.GenerateMapAsync(request.MapSize, request.Progress, request.Run.Cts.Token);
        return new DefaultMapResult(map, biomeProvider);
    }
}
