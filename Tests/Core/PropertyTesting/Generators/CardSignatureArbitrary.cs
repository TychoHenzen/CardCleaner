using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

/// <summary>
///     FsCheck arbitrary generator for CardSignature.
///     Generates valid 8-dimensional signatures with values in [-1, 1].
/// </summary>
public static class CardSignatureArbitrary
{
    /// <summary>
    ///     Gets an Arbitrary that generates CardSignature instances with values in [-1, 1].
    ///     Shrinking moves values toward zero for minimal counterexamples.
    /// </summary>
    public static Arbitrary<CardSignature> Default => Arb.From(Generator, Shrinker);

    /// <summary>
    ///     Generator that produces random CardSignature instances.
    ///     Each dimension is uniformly distributed in [-1, 1].
    /// </summary>
    public static Gen<CardSignature> Generator =>
        from elements in Gen.ArrayOf(8, Gen.Choose(-1000, 1000).Select(i => i / 1000f))
        select new CardSignature(elements);

    /// <summary>
    ///     Shrinker that moves CardSignature values toward zero.
    ///     This helps FsCheck find minimal counterexamples.
    /// </summary>
    public static IEnumerable<CardSignature> Shrinker(CardSignature signature)
    {
        var elements = signature.Elements;

        // Try setting each non-zero dimension to zero
        for (var i = 0; i < 8; i++)
            if (Math.Abs(elements[i]) > 0.001f)
            {
                var shrunk = (float[])elements.Clone();
                shrunk[i] = 0f;
                yield return new CardSignature(shrunk);
            }

        // Try halving each dimension toward zero
        for (var i = 0; i < 8; i++)
            if (Math.Abs(elements[i]) > 0.01f)
            {
                var shrunk = (float[])elements.Clone();
                shrunk[i] = elements[i] / 2f;
                yield return new CardSignature(shrunk);
            }

        // Try the all-zeros signature if not already
        if (elements.Any(e => Math.Abs(e) > 0.001f)) yield return new CardSignature(new float[8]);
    }

    /// <summary>
    ///     Registers this arbitrary with FsCheck's global registry.
    ///     Call this once before running property tests that use CardSignature.
    /// </summary>
    public static void Register() => Arb.Register<CardSignatureArbitraries>();

    /// <summary>
    ///     FsCheck arbitrary provider class for automatic registration.
    /// </summary>
    private sealed class CardSignatureArbitraries
    {
        public static Arbitrary<CardSignature> CardSignature => Default;
    }
}

/// <summary>
///     Additional generators for CardSignature-related testing.
/// </summary>
public static class CardSignatureGenerators
{
    /// <summary>
    ///     Generates a CardSignature with extreme values (-1 or 1 for each dimension).
    /// </summary>
    public static Gen<CardSignature> Extreme =>
        from elements in Gen.ArrayOf(8, Gen.Elements(-1f, 1f))
        select new CardSignature(elements);

    /// <summary>
    ///     Generates a CardSignature with mostly zeros and a few significant values.
    /// </summary>
    public static Gen<CardSignature> Sparse =>
        from activeCount in Gen.Choose(1, 3)
        from activeIndices in Gen.ArrayOf(activeCount, Gen.Choose(0, 7))
        from activeValues in Gen.ArrayOf(activeCount, Gen.Choose(-1000, 1000).Select(i => i / 1000f))
        select CreateSparse(activeIndices, activeValues);

    /// <summary>
    ///     Generates a pair of CardSignatures for testing binary operations.
    /// </summary>
    public static Gen<(CardSignature A, CardSignature B)> Pair =>
        from a in CardSignatureArbitrary.Generator
        from b in CardSignatureArbitrary.Generator
        select (a, b);

    /// <summary>
    ///     Generates a CardSignature with values biased toward a specific range.
    ///     Useful for testing edge cases.
    /// </summary>
    /// <param name="minValue">Minimum value for each dimension.</param>
    /// <param name="maxValue">Maximum value for each dimension.</param>
    public static Gen<CardSignature> InRange(float minValue, float maxValue)
    {
        var min = (int)(Math.Max(-1f, minValue) * 1000);
        var max = (int)(Math.Min(1f, maxValue) * 1000);

        return from elements in Gen.ArrayOf(8, Gen.Choose(min, max).Select(i => i / 1000f))
            select new CardSignature(elements);
    }

    private static CardSignature CreateSparse(int[] indices, float[] values)
    {
        var elements = new float[8];
        for (var i = 0; i < Math.Min(indices.Length, values.Length); i++) elements[indices[i]] = values[i];

        return new CardSignature(elements);
    }
}
