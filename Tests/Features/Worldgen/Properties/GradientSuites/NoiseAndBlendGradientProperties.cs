using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

/// <summary>
///     Property-based tests for noise gradients and manual signature interpolation.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class NoiseAndBlendGradientProperties : GradientPropertyBase
{
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
