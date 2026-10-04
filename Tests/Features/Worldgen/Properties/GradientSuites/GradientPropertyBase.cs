using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Tests.Core.PropertyTesting;
using CardCleaner.Tests.Core.PropertyTesting.Generators;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Properties.GradientSuites;

/// <summary>
///     Shared fixture and generators for the gradient property suites.
/// </summary>
public abstract class GradientPropertyBase : PropertyTestBase
{
    protected const float Epsilon = 0.001f;

    [BeforeTest]
    public new void SetupPropertyTest()
    {
        base.SetupPropertyTest();
        CardSignatureArbitrary.Register();
        GradientArbitraries.Register();
    }

    #region Generators

    protected static Gen<(RadialGradient Gradient, Vector2I Position, Vector2I MapSize)> RadialGradientWithPos =>
        from gradient in GradientArbitraries.RadialGradient
        from posMap in GradientArbitraries.PositionWithMapSize
        select (gradient, posMap.Position, posMap.MapSize);

    protected static Gen<(NoiseGradient Gradient, Vector2I Position, Vector2I MapSize)> NoiseGradientWithPos =>
        from gradient in GradientArbitraries.NoiseGradient
        from posMap in GradientArbitraries.PositionWithMapSize
        select (gradient, posMap.Position, posMap.MapSize);

    protected static Gen<RadialGradientWithSizeInput> RadialGradientWithSize =>
        from gradient in GradientArbitraries.RadialGradient
        from size in Gen.Choose(10, 30)
        select new RadialGradientWithSizeInput(gradient, size);

    protected static Gen<(CardSignature BaseSig, int Seed, Vector2I Position, Vector2I MapSize)> NoiseInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from seed in Gen.Choose(1, 100000)
        from posMap in GradientArbitraries.PositionWithMapSize
        select (baseSig, seed, posMap.Position, posMap.MapSize);

    protected static Gen<(CardSignature BaseSig, float Strength, int Seed, Vector2I Position, Vector2I MapSize)>
        NoiseDeviationInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from strength in Gen.Choose(1, 50).Select(i => i / 100f)
        from seed in Gen.Choose(1, 100000)
        from posMap in GradientArbitraries.PositionWithMapSize
        select (baseSig, strength, seed, posMap.Position, posMap.MapSize);

    protected static Gen<NoiseVariationInput> NoiseVariationInputs =>
        from baseSig in CardSignatureArbitrary.Generator
        from seed in Gen.Choose(1, 100000)
        select new NoiseVariationInput(baseSig, seed);

    protected static Gen<(CardSignature A, CardSignature B, float T)> LerpInputs =>
        from a in CardSignatureArbitrary.Generator
        from b in CardSignatureArbitrary.Generator
        from t in GradientArbitraries.BlendFactor
        select (a, b, t);

    protected readonly record struct RadialGradientWithSizeInput(RadialGradient Gradient, int Size);

    protected readonly record struct NoiseVariationInput(CardSignature BaseSig, int Seed);

    #endregion
}
