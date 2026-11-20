// File: SignatureCardHelperTest.cs

using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features.Card.Services;

[TestSuite]
public class SignatureCardHelperTest
{
    [TestCase]
    public static void TestComputeSeed_AllZeros()
    {
        var signature = new CardSignature();
        var seed = SignatureCardHelper.ComputeSeed(signature);
        Assertions.AssertThat(seed).IsEqual(1331286749777);
    }

    // Rarity is the number of pure elements (magnitude at least 0.9).
    // No pure element: Common, however strong the rest.
    [TestCase(new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }, CardRarity.Common)]
    [TestCase(new[] { 0.89f, -0.89f, 0.89f, -0.89f, 0.89f, -0.89f, 0.89f, -0.89f }, CardRarity.Common)]
    // One pure element: Uncommon.
    [TestCase(new[] { 0.9f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, CardRarity.Uncommon)]
    [TestCase(new[] { 0.5f, 0.5f, 0.5f, -0.95f, 0.5f, 0.5f, 0.5f, 0.5f }, CardRarity.Uncommon)]
    // Two: Rare (the spec's scenario).
    [TestCase(new[] { 0.95f, -0.92f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }, CardRarity.Rare)]
    // Three: Epic.
    [TestCase(new[] { 1f, 1f, -1f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }, CardRarity.Epic)]
    // Four or more: Legendary.
    [TestCase(new[] { 1f, 1f, -1f, -1f, 0f, 0f, 0f, 0f }, CardRarity.Legendary)]
    [TestCase(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, CardRarity.Legendary)]
    [TestCase(new[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f }, CardRarity.Legendary)]
    public static void RarityFollowsThePureElementCount(float[] elements, CardRarity expectedRarity)
    {
        var rarity = SignatureCardHelper.DetermineRarity(new[] { new CardSignature(elements) });
        Assertions.AssertThat(rarity).IsEqual(expectedRarity);
    }

    [TestCase]
    public static void ANegativeElementCountsTheSameAsAPositiveOne()
    {
        var positive = new CardSignature(new[] { 0.95f, -0.92f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f });
        var negative = positive.With(0, -0.95f);

        Assertions.AssertThat(SignatureCardHelper.DetermineRarity(new[] { negative }))
            .IsEqual(SignatureCardHelper.DetermineRarity(new[] { positive }));
    }

    [TestCase]
    public static void AllZeroSignatureIsCommon()
    {
        var rarity = SignatureCardHelper.DetermineRarity(new[] { new CardSignature() });
        Assertions.AssertThat(rarity).IsEqual(CardRarity.Common);
    }

    [TestCase]
    public static void AFaintSignatureIsCommon()
    {
        var faint = new CardSignature(new[] { 0.05f, -0.05f, 0.03f, 0f, -0.01f, 0.05f, -0.04f, 0.02f });

        Assertions.AssertThat(SignatureCardHelper.DetermineRarity(new[] { faint })).IsEqual(CardRarity.Common);
    }

    [TestCase]
    public static void MovingAnElementFurtherFromZeroNeverLowersTheRarity()
    {
        var rng = RandomSources.Create(20261120UL);
        for (var sample = 0; sample < 500; sample++)
        {
            var signature = SignatureRandom.Create(rng);
            var element = rng.RandiRange(0, 7);
            var value = signature[element];
            var stronger = signature.With(element, value + (value < 0f ? -1f : 1f) * rng.RandfRange(0f, 1f));

            Assertions.AssertThat((int)SignatureCardHelper.DetermineRarity(new[] { stronger }))
                .IsGreaterEqual((int)SignatureCardHelper.DetermineRarity(new[] { signature }));
        }
    }

    [TestCase]
    public static void UniformSignaturesSpreadOverTheRaritiesAsTheBinomialPredicts()
    {
        // Each element is pure with chance 0.1, so the pure count over 8 elements is binomial(8, 0.1).
        const int samples = 10000;
        var expected = new[] { 43.0, 38.3, 14.9, 3.3, 0.5 };
        var counts = new int[expected.Length];
        var rng = RandomSources.Create(424242UL);
        for (var i = 0; i < samples; i++)
            counts[(int)SignatureCardHelper.DetermineRarity(new[] { SignatureRandom.Create(rng) })]++;

        for (var rarity = 0; rarity < expected.Length; rarity++)
            Assertions.AssertThat(System.Math.Abs(100.0 * counts[rarity] / samples - expected[rarity]))
                .IsLessEqual(2.0);
    }

    [TestCase]
    public static void PickUniformOfOneItemDoesNotDraw()
    {
        var rng = RandomSources.Create(42UL);

        var picked = SignatureCardHelper.PickUniform(rng, new[] { "only" });

        Assertions.AssertThat(picked).IsEqual("only");
        Assertions.AssertThat(rng.Randi()).IsEqual(RandomSources.Create(42UL).Randi());
    }

    [TestCase]
    public static void PickUniformReachesEveryItem()
    {
        var items = new[] { "a", "b", "c" };
        var reached = new HashSet<string>();
        for (ulong seed = 1; seed <= 50; seed++)
            reached.Add(SignatureCardHelper.PickUniform(RandomSources.Create(seed), items));

        Assertions.AssertThat(reached.Count).IsEqual(3);
    }
}
