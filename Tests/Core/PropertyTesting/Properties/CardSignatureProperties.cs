using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Properties;

/// <summary>
///     Property-based tests validating CardSignature invariants.
///     Tests that signatures maintain their constraints under all operations.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardSignatureProperties : PropertyTestBase
{
    private const float Epsilon = 0.0001f;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
    }

    #region Named Property Invariants

    [TestCase]
    public void NamedPropertiesMatchIndexers()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                return Mathf.Abs(sig.Solidum - sig[0]) < Epsilon &&
                       Mathf.Abs(sig.Febris - sig[1]) < Epsilon &&
                       Mathf.Abs(sig.Ordinem - sig[2]) < Epsilon &&
                       Mathf.Abs(sig.Lumines - sig[3]) < Epsilon &&
                       Mathf.Abs(sig.Varias - sig[4]) < Epsilon &&
                       Mathf.Abs(sig.Inertiae - sig[5]) < Epsilon &&
                       Mathf.Abs(sig.Subsidium - sig[6]) < Epsilon &&
                       Mathf.Abs(sig.Spatium - sig[7]) < Epsilon;
            })
            .Iterations(500));
    }

    #endregion

    #region Generators

    private static Gen<(int Index, float Value)> IndexValueParams =>
        from index in Gen.Choose(0, 7)
        from value in Gen.Choose(-5000, 5000).Select(i => i / 1000f)
        select (index, value);

    private static Gen<(CardSignature A, CardSignature B, CardSignature C)> ThreeSignatures =>
        from a in CardSignatureArbitrary.Generator
        from b in CardSignatureArbitrary.Generator
        from c in CardSignatureArbitrary.Generator
        select (a, b, c);

    #endregion

    #region Bounds Invariants

    [TestCase]
    public void AllDimensionsAreWithinBounds()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                var elements = sig.Elements;
                return elements.All(e => e >= -1f && e <= 1f);
            })
            .Iterations(1000));
    }

    [TestCase]
    public void ConstructorClampsBoundsOnOutOfRangeValues()
    {
        Property(p => p
            .ForAll<float[]>(
                Arb.From(Gen.ArrayOf(Gen.Choose(-5000, 5000).Select(i => i / 1000f), 8)),
                unclamped =>
                {
                    var sig = new CardSignature(unclamped);
                    var elements = sig.Elements;
                    return elements.All(e => e >= -1f && e <= 1f);
                })
            .Iterations(500));
    }

    [TestCase]
    public void SetterClampsBoundsOnOutOfRangeValues()
    {
        Property(p => p
            .ForAll(
                Arb.From(Gen.Choose(-5000, 5000).Select(i => i / 1000f)),
                value =>
                {
                    var sig = new CardSignature();
                    sig.Solidum = value;
                    return sig.Solidum >= -1f && sig.Solidum <= 1f;
                })
            .Iterations(500));
    }

    [TestCase]
    public void IndexerSetterClampsBounds()
    {
        Property(p => p
            .ForAll(
                Arb.From(IndexValueParams),
                args =>
                {
                    var sig = new CardSignature();
                    sig[args.Index] = args.Value;
                    return sig[args.Index] >= -1f && sig[args.Index] <= 1f;
                })
            .Iterations(500));
    }

    #endregion

    #region Magnitude Invariants

    [TestCase]
    public void MagnitudeNeverExceedsMaximum()
    {
        // In an 8D unit hypercube [-1,1]^8, the maximum distance from origin
        // is sqrt(8) ≈ 2.83 (when all dimensions are at ±1)
        var maxMagnitude = Mathf.Sqrt(8) + Epsilon;

        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                var origin = new CardSignature(new float[8]);
                var magnitude = sig.DistanceTo(origin);
                return magnitude <= maxMagnitude;
            })
            .Iterations(1000));
    }

    [TestCase]
    public void ExtremeSignatureHasMaximumMagnitude()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Extreme),
                sig =>
                {
                    var origin = new CardSignature(new float[8]);
                    var magnitude = sig.DistanceTo(origin);
                    // All ±1 values should give exactly sqrt(8)
                    return Mathf.Abs(magnitude - Mathf.Sqrt(8)) < Epsilon;
                })
            .Iterations(100));
    }

    #endregion

    #region Distance Invariants

    [TestCase]
    public void DistanceToSelfIsZero()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                return sig.DistanceTo(sig) < Epsilon;
            })
            .Iterations(1000));
    }

    [TestCase]
    public void DistanceIsSymmetric()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    var d1 = pair.A.DistanceTo(pair.B);
                    var d2 = pair.B.DistanceTo(pair.A);
                    return Mathf.Abs(d1 - d2) < Epsilon;
                })
            .Iterations(500));
    }

    [TestCase]
    public void DistanceIsNonNegative()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    var distance = pair.A.DistanceTo(pair.B);
                    return distance >= 0f;
                })
            .Iterations(500));
    }

    [TestCase]
    public void TriangleInequalityHolds()
    {
        Property(p => p
            .ForAll(
                Arb.From(ThreeSignatures),
                sigs =>
                {
                    var ab = sigs.A.DistanceTo(sigs.B);
                    var bc = sigs.B.DistanceTo(sigs.C);
                    var ac = sigs.A.DistanceTo(sigs.C);
                    // d(a,c) <= d(a,b) + d(b,c)
                    return ac <= ab + bc + Epsilon;
                })
            .Iterations(500));
    }

    #endregion

    #region Subtract Invariants

    [TestCase]
    public void SubtractResultIsWithinBounds()
    {
        Property(p => p
            .ForAll(
                Arb.From(CardSignatureGenerators.Pair),
                pair =>
                {
                    var result = pair.A.Subtract(pair.B);
                    var elements = result.Elements;
                    return elements.All(e => e >= -1f && e <= 1f);
                })
            .Iterations(500));
    }

    [TestCase]
    public void SubtractFromSelfIsZero()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                var result = sig.Subtract(sig);
                var elements = result.Elements;
                return elements.All(e => Mathf.Abs(e) < Epsilon);
            })
            .Iterations(500));
    }

    #endregion

    #region Indexing Invariants

    [TestCase]
    public void AllIndicesAreAccessible()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                // All 8 indices should be accessible without exception
                for (var i = 0; i < 8; i++)
                {
                    var _ = sig[i];
                }

                return true;
            })
            .Iterations(500));
    }

    [TestCase]
    public void ElementsPropertyReturnsClone()
    {
        Property(p => p
            .ForAll(CardSignatureArbitrary.Default, sig =>
            {
                var elements = sig.Elements;
                var original = sig.Elements[0];
                elements[0] = -999f; // Modify the returned array
                // Original should be unchanged
                return Mathf.Abs(sig.Elements[0] - original) < Epsilon;
            })
            .Iterations(500));
    }

    #endregion
}
