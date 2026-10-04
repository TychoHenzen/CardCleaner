using System;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using CardCleaner.Tests.Mocks;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Support;

/// <summary>
///     Shared fixture for property suites that generate maps through SimpleMapGenerator.
/// </summary>
public abstract class MapGenerationPropertyBase : PropertyTestBase
{
    private BiomeRegistry _registry = null!;
    private MockTileRegistry _tileRegistry = null!;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _tileRegistry = MockTileRegistry.CreateWithTestTiles();
    }

    protected static Gen<MapRequest> MapRequests(int minSize = 5, int maxSize = 15) =>
        from seed in Gen.Choose(1, 1000000)
        from width in Gen.Choose(minSize, maxSize)
        from height in Gen.Choose(minSize, maxSize)
        select new MapRequest(seed, width, height);

    protected static Gen<SignedMapRequest> SignedMapRequests(int minSize, int maxSize) =>
        from request in MapRequests(minSize, maxSize)
        from signature in CardSignatureArbitrary.Generator
        select new SignedMapRequest(request, signature);

    /// <summary>
    ///     Runs <paramref name="check" /> against generated map requests with sizes in [minSize, maxSize].
    /// </summary>
    protected void MapProperty(int minSize, int maxSize, int iterations, Func<MapRequest, bool> check)
    {
        Property(p => p
            .ForAll(Arb.From(MapRequests(minSize, maxSize)), check)
            .Iterations(iterations));
    }

    /// <summary>
    ///     Checks that maps generated with a fixed signature satisfy all core map invariants.
    /// </summary>
    protected void MapInvariantsHoldFor(CardSignature signature, int iterations)
    {
        MapProperty(8, 15, iterations, request =>
            MapInvariants.Validate(GenerateMap(request, signature), request.Width, request.Height));
    }

    protected SimpleMapData GenerateMap(MapRequest request)
    {
        return GenerateMap(request, new CardSignature());
    }

    protected SimpleMapData GenerateMap(MapRequest request, CardSignature signature)
    {
        var rng = new RandomNumberGenerator { Seed = request.RngSeed };
        var gradient = new ConstantSignatureGradient(signature);
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, request.Size);
        var generator = new SimpleMapGenerator(rng, biomeProvider, _tileRegistry, _tileRegistry);
        return generator.GenerateMap(request.Size);
    }
}
