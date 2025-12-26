using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties;

/// <summary>
///     Property-based tests verifying determinism of procedural generators.
///     Given the same seed and inputs, generators must produce identical output.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DeterminismProperties : PropertyTestBase
{
    private BiomeRegistry _registry = null!;
    private ITileRegistry _tileRegistry = null!;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
        _registry = new BiomeRegistry();
        _registry.RegisterDefaultBiomes();
        _tileRegistry = new TileRegistry();
    }

    /// <summary>
    ///     Simple gradient that returns a constant signature at all positions.
    /// </summary>
    private sealed partial class ConstantGradient : BaselineGradient
    {
        private readonly CardSignature _signature;

        public ConstantGradient(CardSignature signature)
        {
            _signature = signature;
        }

        public override CardSignature GetSignatureAt(Vector2I position, Vector2I mapSize) => _signature;
    }

    #region Generators

    private static Gen<(int Seed, int Width, int Height)> MapGenParams(int minSize = 5, int maxSize = 15) =>
        from seed in Gen.Choose(1, 1000000)
        from width in Gen.Choose(minSize, maxSize)
        from height in Gen.Choose(minSize, maxSize)
        select (seed, width, height);

    private static Gen<(int Seed, int Count)> SeedCountParams =>
        from seed in Gen.Choose(1, 1000000)
        from count in Gen.Choose(1, 10)
        select (seed, count);

    #endregion

    #region SimpleMapGenerator Determinism

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_TileLayout()
    {
        Property(p => p
            .ForAll(
                Arb.From(MapGenParams()),
                args =>
                {
                    var size = new Vector2I(args.Width, args.Height);

                    var map1 = GenerateMapWithSeed((ulong)args.Seed, size);
                    var map2 = GenerateMapWithSeed((ulong)args.Seed, size);

                    return TileLayoutsMatch(map1, map2, size);
                })
            .Iterations(100));
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_PlayerStart()
    {
        Property(p => p
            .ForAll(
                Arb.From(MapGenParams()),
                args =>
                {
                    var size = new Vector2I(args.Width, args.Height);

                    var map1 = GenerateMapWithSeed((ulong)args.Seed, size);
                    var map2 = GenerateMapWithSeed((ulong)args.Seed, size);

                    return map1.PlayerStart == map2.PlayerStart;
                })
            .Iterations(100));
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_EnemyPositions()
    {
        Property(p => p
            .ForAll(
                Arb.From(MapGenParams(8)),
                args =>
                {
                    var size = new Vector2I(args.Width, args.Height);

                    var map1 = GenerateMapWithSeed((ulong)args.Seed, size);
                    var map2 = GenerateMapWithSeed((ulong)args.Seed, size);

                    if (map1.EnemyPositions.Count != map2.EnemyPositions.Count)
                        return false;

                    return map1.EnemyPositions.SequenceEqual(map2.EnemyPositions);
                })
            .Iterations(100));
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_PassableTileCount()
    {
        Property(p => p
            .ForAll(
                Arb.From(MapGenParams(5, 20)),
                args =>
                {
                    var size = new Vector2I(args.Width, args.Height);

                    var map1 = GenerateMapWithSeed((ulong)args.Seed, size);
                    var map2 = GenerateMapWithSeed((ulong)args.Seed, size);

                    return map1.PassableTiles.Count == map2.PassableTiles.Count;
                })
            .Iterations(100));
    }

    #endregion

    #region CardSignature.Random Determinism

    [TestCase]
    public void CardSignatureRandomIsDeterministic()
    {
        Property(p => p
            .ForAll(
                Arb.From(Gen.Choose(1, 1000000)),
                seed =>
                {
                    var rng1 = new RandomNumberGenerator { Seed = (ulong)seed };
                    var rng2 = new RandomNumberGenerator { Seed = (ulong)seed };

                    var sig1 = CardSignature.Random(rng1);
                    var sig2 = CardSignature.Random(rng2);

                    return sig1.DistanceTo(sig2) < 0.0001f;
                })
            .Iterations(200));
    }

    [TestCase]
    public void CardSignatureRandomSequenceIsDeterministic()
    {
        Property(p => p
            .ForAll(
                Arb.From(SeedCountParams),
                args =>
                {
                    var rng1 = new RandomNumberGenerator { Seed = (ulong)args.Seed };
                    var rng2 = new RandomNumberGenerator { Seed = (ulong)args.Seed };

                    for (var i = 0; i < args.Count; i++)
                    {
                        var sig1 = CardSignature.Random(rng1);
                        var sig2 = CardSignature.Random(rng2);

                        if (sig1.DistanceTo(sig2) > 0.0001f)
                            return false;
                    }

                    return true;
                })
            .Iterations(100));
    }

    #endregion

    #region Helper Methods

    private SimpleMapData GenerateMapWithSeed(ulong seed, Vector2I size)
    {
        var rng = new RandomNumberGenerator { Seed = seed };
        var gradient = new ConstantGradient(new CardSignature());
        var biomeProvider = new BiomeMapGenerator(_registry, gradient, size);
        var generator = new SimpleMapGenerator(rng, biomeProvider, _tileRegistry);
        return generator.GenerateMap(size);
    }

    private static bool TileLayoutsMatch(SimpleMapData map1, SimpleMapData map2, Vector2I size)
    {
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
            if (map1.TileIds[y, x] != map2.TileIds[y, x])
                return false;

        return true;
    }

    #endregion
}
