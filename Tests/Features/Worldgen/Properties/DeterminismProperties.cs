using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.Features.Worldgen.Support;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties;

/// <summary>
///     Property-based tests verifying determinism of procedural generators.
///     Given the same seed and inputs, generators must produce identical output.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public partial class DeterminismProperties : MapGenerationPropertyBase
{
    private const float SignatureTolerance = 0.0001f;

    #region SimpleMapGenerator Determinism

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_TileLayout()
    {
        TwiceGeneratedMapsMatch(5, 15, (map1, map2, request) => TileLayoutsMatch(map1, map2, request.Size));
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_PlayerStart()
    {
        TwiceGeneratedMapsMatch(5, 15, (map1, map2, _) => map1.PlayerStart == map2.PlayerStart);
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_EnemyPositions()
    {
        TwiceGeneratedMapsMatch(8, 15, (map1, map2, _) =>
            map1.EnemyPositions.Count == map2.EnemyPositions.Count &&
            map1.EnemyPositions.SequenceEqual(map2.EnemyPositions));
    }

    [TestCase]
    public void SimpleMapGeneratorIsDeterministic_PassableTileCount()
    {
        TwiceGeneratedMapsMatch(5, 20, (map1, map2, _) => map1.PassableTiles.Count == map2.PassableTiles.Count);
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

                    return sig1.DistanceTo(sig2) < SignatureTolerance;
                })
            .Iterations(200));
    }

    [TestCase]
    public void CardSignatureRandomSequenceIsDeterministic()
    {
        var seedAndCount =
            from seed in Gen.Choose(1, 1000000)
            from count in Gen.Choose(1, 10)
            select new SeedAndCount(seed, count);

        Property(p => p
            .ForAll(
                Arb.From(seedAndCount),
                args =>
                {
                    var rng1 = new RandomNumberGenerator { Seed = (ulong)args.Seed };
                    var rng2 = new RandomNumberGenerator { Seed = (ulong)args.Seed };

                    return Enumerable.Range(0, args.Count)
                        .All(_ => CardSignature.Random(rng1)
                            .DistanceTo(CardSignature.Random(rng2)) <= SignatureTolerance);
                })
            .Iterations(100));
    }

    #endregion

    #region Helper Methods

    private readonly record struct SeedAndCount(int Seed, int Count);

    private void TwiceGeneratedMapsMatch(
        int minSize,
        int maxSize,
        Func<SimpleMapData, SimpleMapData, MapRequest, bool> matches)
    {
        MapProperty(minSize, maxSize, 100, request => matches(GenerateMap(request), GenerateMap(request), request));
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
