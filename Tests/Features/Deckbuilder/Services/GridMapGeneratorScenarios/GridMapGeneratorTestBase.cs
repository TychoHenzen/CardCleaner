using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.GridMapGeneration;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.Mocks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.GridMapGeneratorScenarios;

/// <summary>
///     Shared fixture for the grid map generator scenario suites.
/// </summary>
public abstract class GridMapGeneratorTestBase
{
    private protected GridMapGenerationPipeline _generator = null!;
    protected BiomeRegistry _registry = null!;
    protected MockTileRegistry _tileRegistry = null!;
    protected IRandom _rng = null!;

    [BeforeTest]
    public void Setup()
    {
        _rng = RandomSources.Create();
        _rng.Seed = 12345;
        _tileRegistry = MockTileRegistry.CreateWithTestTiles();

        // Create biomes that use the mock tiles
        _registry = new BiomeRegistry();
        RegisterMockBiomes();

        var gradient = new CardBasedGradient(new[] { new CardSignature() }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry.ToBiomeSet(),gradient.GetSignatureAt, new Vector2I(10, 10));
        _generator = GridMapGenerator.CreatePipeline(_rng, biomeProvider, _tileRegistry, _tileRegistry);
    }

    protected void RegisterMockBiomes()
    {
        // Create biomes that use the mock tile IDs (grass, stone, dirt, rock)
        var passablePool = new TilePool.Builder();
        passablePool.Add("grass", 1.0f);
        passablePool.Add("dirt", 1.0f);
        var blockedPool = new TilePool.Builder();
        blockedPool.Add("stone", 1.0f);
        blockedPool.Add("rock", 1.0f);

        // Register a single test biome - all positions will use this
        var signature = new CardSignature();
        _registry.Register(new BiomeDefinition("test_biome", signature, passablePool.Build(), blockedPool.Build()));
    }

    private protected GridMapGenerationPipeline CreateGenerator(Vector2I mapSize, CardSignature? signature = null)
    {
        var seed = signature ?? new CardSignature();
        var gradient = new CardBasedGradient(new[] { seed }, _rng);
        var biomeProvider = new BiomeMapGenerator(_registry.ToBiomeSet(),gradient.GetSignatureAt, mapSize);
        return GridMapGenerator.CreatePipeline(_rng, biomeProvider, _tileRegistry, _tileRegistry);
    }
}
