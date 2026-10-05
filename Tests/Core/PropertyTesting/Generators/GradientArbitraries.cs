using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using FsCheck;
using Godot;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck arbitrary generators for gradient system testing.
/// </summary>
public static class GradientArbitraries
{
    /// <summary>
    ///     Generates a position and map size pair where position is always valid.
    /// </summary>
    public static Gen<(Vector2I Position, Vector2I MapSize)> PositionWithMapSize =>
        from width in Gen.Choose(5, 50)
        from height in Gen.Choose(5, 50)
        from x in Gen.Choose(0, width - 1)
        from y in Gen.Choose(0, height - 1)
        select (new Vector2I(x, y), new Vector2I(width, height));

    /// <summary>
    ///     Generates a blend factor (t) in [0, 1] range for testing interpolation.
    /// </summary>
    public static Gen<float> BlendFactor =>
        Gen.Choose(0, 1000).Select(i => i / 1000f);

    /// <summary>
    ///     Generates a RadialGradient with valid configuration.
    /// </summary>
    public static Gen<RadialGradient> RadialGradient =>
        from center in CardSignatureArbitrary.Generator
        from edge in CardSignatureArbitrary.Generator
        from falloff in Gen.Choose(1, 30).Select(i => i / 10f) // 0.1 to 3.0
        select new RadialGradient { CenterSignature = center, EdgeSignature = edge, Falloff = falloff };

    /// <summary>
    ///     Generates a NoiseGradient with valid configuration.
    /// </summary>
    public static Gen<NoiseGradient> NoiseGradient =>
        from baseSig in CardSignatureArbitrary.Generator
        from scale in Gen.Choose(1, 100).Select(i => i / 100f) // 0.01 to 1.0
        from strength in Gen.Choose(1, 50).Select(i => i / 100f) // 0.01 to 0.5
        from seed in Gen.Choose(1, 100000)
        select new NoiseGradient
        {
            BaseSignature = baseSig, NoiseScale = scale, NoiseStrength = strength, NoiseSeed = seed
        };

    /// <summary>
    ///     Generates a pair of signatures for testing blending operations.
    /// </summary>
    public static Gen<(CardSignature From, CardSignature To, float T)> BlendInputs =>
        from fromSig in CardSignatureArbitrary.Generator
        from toSig in CardSignatureArbitrary.Generator
        from t in BlendFactor
        select (fromSig, toSig, t);

    /// <summary>
    ///     Generates valid Vector2I positions within a given map size.
    /// </summary>
    public static Gen<Vector2I> PositionInMap(int maxWidth, int maxHeight) =>
        from x in Gen.Choose(0, maxWidth - 1)
        from y in Gen.Choose(0, maxHeight - 1)
        select new Vector2I(x, y);

    /// <summary>
    ///     Registers all gradient-related arbitraries with FsCheck.
    /// </summary>
    public static void Register() { }

    private sealed class GradientArbitraryProvider
    {
        public static Arbitrary<RadialGradient> RadialGradientArb =>
            Arb.From(RadialGradient);

        public static Arbitrary<NoiseGradient> NoiseGradientArb =>
            Arb.From(NoiseGradient);
    }
}
