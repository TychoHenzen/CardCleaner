using System;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using FsCheck;

namespace CardCleaner.Tests.Core.PropertyTesting.Generators;

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
