// File: SignatureCardHelperTest.cs

using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features.Card.Services;

[TestSuite]
[RequireGodotRuntime]
public class SignatureCardHelperTest
{
    [TestCase]
    public static void TestComputeSeed_AllZeros()
    {
        var signature = new CardSignature();
        var seed = SignatureCardHelper.ComputeSeed(signature);
        Assertions.AssertThat(seed).IsEqual(1331286749777);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEffectSeedIsTheSignatureSeedKeptBelowTenThousand()
    {
        var signature = new CardSignature();

        var seed = SignatureCardHelper.EffectSeed(signature);

        // 1331286749777 is the all-zero ComputeSeed above; the shader needs a seed small enough for sin().
        Assertions.AssertThat(seed).IsEqual(9777f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEffectSeedDependsOnlyOnTheSignature()
    {
        var first = SignatureCardHelper.EffectSeed(new CardSignature { Solidum = 0.7f, Febris = -0.5f });
        var again = SignatureCardHelper.EffectSeed(new CardSignature { Solidum = 0.7f, Febris = -0.5f });
        var otherSignature = new CardSignature { Solidum = 0.5f, Febris = -0.3f, Ordinem = 0.8f };
        var other = SignatureCardHelper.EffectSeed(otherSignature);

        Assertions.AssertThat(again).IsEqual(first);
        Assertions.AssertThat(first).IsEqual(8177f);
        Assertions.AssertThat(other).IsEqual(977f);
    }

    // Common (ratio < 0.4458)
    [TestCase(new[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f }, CardRarity.Common)]
    [TestCase(new[] { -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f }, CardRarity.Common)]
    [TestCase(new[] { 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f }, CardRarity.Common)]
    [TestCase(new[] { -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f }, CardRarity.Common)]
    [TestCase(new[] { 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f }, CardRarity.Common)]

    // Uncommon (0.4458 <= ratio < 0.58996)
    [TestCase(new[] { 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f }, CardRarity.Uncommon)]
    [TestCase(new[] { -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f }, CardRarity.Uncommon)]
    [TestCase(new[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f }, CardRarity.Uncommon)]
    [TestCase(new[] { -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f }, CardRarity.Uncommon)]
    [TestCase(new[] { 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.2f }, CardRarity.Uncommon)]

    // Rare (0.58996 <= ratio < 0.81307)
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f }, CardRarity.Rare)]
    [TestCase(new[] { 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f }, CardRarity.Rare)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f }, CardRarity.Rare)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f }, CardRarity.Rare)]
    [TestCase(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -0.15f, -0.15f, -0.15f, -0.15f }, CardRarity.Rare)]

    // Epic (0.81307 <= ratio < 0.90223)
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f }, CardRarity.Epic)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f }, CardRarity.Epic)]
    [TestCase(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, 0.5f }, CardRarity.Epic)]
    [TestCase(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -0.85f, -0.85f }, CardRarity.Epic)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.75f }, CardRarity.Epic)]

    // Legendary (ratio >= 0.90223)
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f }, CardRarity.Legendary)]
    [TestCase(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f }, CardRarity.Legendary)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f }, CardRarity.Legendary)]
    [TestCase(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.75f }, CardRarity.Legendary)]
    [TestCase(new[] { -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -0.75f }, CardRarity.Legendary)]
    public static void TestDetermineRarity_Param(float[] elements, CardRarity expectedRarity)
    {
        var signature = new CardSignature(elements);
        var rarity = SignatureCardHelper.DetermineRarity(new[] { signature });
        Assertions.AssertThat(rarity).IsEqual(expectedRarity);
    }

    [TestCase]
    public static void TestDetermineRarity_AllZeroSignatureIsCommon()
    {
        var rarity = SignatureCardHelper.DetermineRarity(new[] { new CardSignature() });
        Assertions.AssertThat(rarity).IsEqual(CardRarity.Common);
    }
}
