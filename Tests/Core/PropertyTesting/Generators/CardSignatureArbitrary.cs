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
