using System.Linq;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

/// <summary>
///     Property-based tests for output bounds and radial gradient behavior.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RadialGradientProperties : GradientPropertyBase
{
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
}
