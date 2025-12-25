using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties;

/// <summary>
///     Property-based tests for gradient systems.
///     Validates that gradients produce valid signatures and exhibit expected behavior.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GradientProperties : PropertyTestBase
{
    private const float Epsilon = 0.001f;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
        GradientArbitraries.Register();
    }

    #region Generators

    private static Gen<(RadialGradient Gradient, Vector2I Position, Vector2I MapSize)> RadialGradientWithPos =>
        from gradient in GradientArbitraries.RadialGradient
        from posMap in GradientArbitraries.PositionWithMapSize
        select (gradient, posMap.Position, posMap.MapSize);

    private static Gen<(NoiseGradient Gradient, Vector2I Position, Vector2I MapSize)> NoiseGradientWithPos =>
        from gradient in GradientArbitraries.NoiseGradient
        from posMap in GradientArbitraries.PositionWithMapSize
        select (gradient, posMap.Position, posMap.MapSize);

    private static Gen<(RadialGradient Gradient, int Size)> RadialGradientWithSize =>
        from gradient in GradientArbitraries.RadialGradient
        from size in Gen.Choose(10, 30)
        select (gradient, size);

    private static Gen<(CardSignature BaseSig, int Seed, Vector2I Position, Vector2I MapSize)> NoiseInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from seed in Gen.Choose(1, 100000)
        from posMap in GradientArbitraries.PositionWithMapSize
        select (baseSig, seed, posMap.Position, posMap.MapSize);

    private static Gen<(CardSignature BaseSig, float Strength, int Seed, Vector2I Position, Vector2I MapSize)>
        NoiseDeviationInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from strength in Gen.Choose(1, 50).Select(i => i / 100f)
        from seed in Gen.Choose(1, 100000)
        from posMap in GradientArbitraries.PositionWithMapSize
        select (baseSig, strength, seed, posMap.Position, posMap.MapSize);

    private static Gen<(CardSignature BaseSig, int Seed)> NoiseVariationInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from seed in Gen.Choose(1, 100000)
        select (baseSig, seed);

    private static Gen<(CardSignature A, CardSignature B, float T)> LerpInputs =>
        from a in CardSignatureArbitrary.Generator
        from b in CardSignatureArbitrary.Generator
        from t in GradientArbitraries.BlendFactor
        select (a, b, t);

    #endregion

    #region Bounds Properties

    [TestCase]
    public void RadialGradientOutputIsWithinBounds()
    {
        Property(p => p
            .ForAll(
                Arb.From(RadialGradientWithPos),
                args =>
                {
                    var sig = args.Gradient.GetSignatureAt(args.Position, args.MapSize);
                    var elements = sig.Elements;
                    return elements.All(e => e >= -1f && e <= 1f);
                })
            .Iterations(500));
    }

    [TestCase]
    public void NoiseGradientOutputIsWithinBounds()
    {
        Property(p => p
            .ForAll(
                Arb.From(NoiseGradientWithPos),
                args =>
                {
                    var sig = args.Gradient.GetSignatureAt(args.Position, args.MapSize);
                    var elements = sig.Elements;
                    return elements.All(e => e >= -1f && e <= 1f);
                })
            .Iterations(500));
    }

    #endregion

    #region RadialGradient Behavior Properties

    [TestCase]
    public void RadialGradientCenterReturnsApproximatelyCenterSignature()
    {
        Property(p => p
            .ForAll(
                Arb.From(RadialGradientWithSize),
                args =>
                {
                    var mapSize = new Vector2I(args.Size, args.Size);
                    var center = new Vector2I(args.Size / 2, args.Size / 2);

                    var sig = args.Gradient.GetSignatureAt(center, mapSize);

                    // At exact center, should be very close to CenterSignature
                    var distance = sig.DistanceTo(args.Gradient.CenterSignature);
                    return distance < 0.5f;
                })
            .Iterations(200));
    }

    [TestCase]
    public void RadialGradientCornerApproachesEdgeSignature()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    var gradient = new RadialGradient
                    {
                        CenterSignature = pair.A, EdgeSignature = pair.B, Falloff = 1.0f
                    };

                    var mapSize = new Vector2I(20, 20);
                    var corner = new Vector2I(0, 0); // Furthest from center

                    var sig = gradient.GetSignatureAt(corner, mapSize);

                    // At corner, should be closer to edge than center
                    var distToCenter = sig.DistanceTo(pair.A);
                    var distToEdge = sig.DistanceTo(pair.B);
                    return distToEdge <= distToCenter + Epsilon;
                })
            .Iterations(200));
    }

    [TestCase]
    public void RadialGradientInterpolationIsContinuous()
    {
        Property(p => p
            .ForAll(
                Arb.From(RadialGradientWithSize),
                args =>
                {
                    var mapSize = new Vector2I(args.Size, args.Size);

                    // Check that adjacent positions produce similar signatures
                    for (var x = 0; x < args.Size - 1; x++)
                    {
                        var pos1 = new Vector2I(x, args.Size / 2);
                        var pos2 = new Vector2I(x + 1, args.Size / 2);

                        var sig1 = args.Gradient.GetSignatureAt(pos1, mapSize);
                        var sig2 = args.Gradient.GetSignatureAt(pos2, mapSize);

                        var distance = sig1.DistanceTo(sig2);
                        if (distance > 1.0f) return false;
                    }

                    return true;
                })
            .Iterations(100));
    }

    #endregion

    #region NoiseGradient Behavior Properties

    [TestCase]
    public void NoiseGradientIsDeterministicWithSameSeed()
    {
        Property(p => p
            .ForAll(
                Arb.From(NoiseInputs),
                args =>
                {
                    var gradient1 = new NoiseGradient
                    {
                        BaseSignature = args.BaseSig, NoiseScale = 0.1f, NoiseStrength = 0.3f, NoiseSeed = args.Seed
                    };

                    var gradient2 = new NoiseGradient
                    {
                        BaseSignature = args.BaseSig, NoiseScale = 0.1f, NoiseStrength = 0.3f, NoiseSeed = args.Seed
                    };

                    var sig1 = gradient1.GetSignatureAt(args.Position, args.MapSize);
                    var sig2 = gradient2.GetSignatureAt(args.Position, args.MapSize);

                    return sig1.DistanceTo(sig2) < Epsilon;
                })
            .Iterations(200));
    }

    [TestCase]
    public void NoiseGradientOutputDeviatesFromBaseWithinStrength()
    {
        Property(p => p
            .ForAll(
                Arb.From(NoiseDeviationInputs),
                args =>
                {
                    var gradient = new NoiseGradient
                    {
                        BaseSignature = args.BaseSig,
                        NoiseScale = 0.1f,
                        NoiseStrength = args.Strength,
                        NoiseSeed = args.Seed
                    };

                    var sig = gradient.GetSignatureAt(args.Position, args.MapSize);

                    // Each dimension should differ by at most NoiseStrength
                    for (var i = 0; i < 8; i++)
                    {
                        var diff = Math.Abs(sig[i] - args.BaseSig[i]);
                        if (diff > args.Strength + Epsilon) return false;
                    }

                    return true;
                })
            .Iterations(300));
    }

    [TestCase]
    public void NoiseGradientProducesVariation()
    {
        Property(p => p
            .ForAll(
                Arb.From(NoiseVariationInputs),
                args =>
                {
                    var gradient = new NoiseGradient
                    {
                        BaseSignature = args.BaseSig, NoiseScale = 0.2f, NoiseStrength = 0.3f, NoiseSeed = args.Seed
                    };

                    var mapSize = new Vector2I(20, 20);

                    // Sample several positions
                    var samples = new[]
                    {
                        gradient.GetSignatureAt(new Vector2I(0, 0), mapSize),
                        gradient.GetSignatureAt(new Vector2I(10, 0), mapSize),
                        gradient.GetSignatureAt(new Vector2I(0, 10), mapSize),
                        gradient.GetSignatureAt(new Vector2I(10, 10), mapSize),
                        gradient.GetSignatureAt(new Vector2I(5, 5), mapSize)
                    };

                    // At least some samples should differ
                    var allSame = samples.All(s => s.DistanceTo(samples[0]) < Epsilon);
                    return !allSame;
                })
            .Iterations(100));
    }

    #endregion

    #region Interpolation Properties (Manual Lerp Testing)

    [TestCase]
    public void ManualLerpEndpointsAreCorrect()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    // t=0 should give a, t=1 should give b
                    var at0 = LerpSignature(pair.A, pair.B, 0f);
                    var at1 = LerpSignature(pair.A, pair.B, 1f);

                    return at0.DistanceTo(pair.A) < Epsilon && at1.DistanceTo(pair.B) < Epsilon;
                })
            .Iterations(500));
    }

    [TestCase]
    public void ManualLerpMidpointIsEquidistant()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    var midpoint = LerpSignature(pair.A, pair.B, 0.5f);
                    var distA = midpoint.DistanceTo(pair.A);
                    var distB = midpoint.DistanceTo(pair.B);

                    return Math.Abs(distA - distB) < Epsilon;
                })
            .Iterations(500));
    }

    [TestCase]
    public void ManualLerpOutputIsWithinBounds()
    {
        Property(p => p
            .ForAll(
                Arb.From(LerpInputs),
                args =>
                {
                    var result = LerpSignature(args.A, args.B, args.T);
                    return result.Elements.All(e => e >= -1f && e <= 1f);
                })
            .Iterations(500));
    }

    private static CardSignature LerpSignature(CardSignature a, CardSignature b, float t)
    {
        var result = new CardSignature();
        for (var i = 0; i < 8; i++) result[i] = Mathf.Lerp(a[i], b[i], t);

        return result;
    }

    #endregion
}
