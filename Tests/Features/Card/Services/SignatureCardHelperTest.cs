// File: SignatureCardHelperTest.cs

using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features;

[TestSuite]
[RequireGodotRuntime]
public class SignatureCardHelperTest
{
    [TestCase]
    public void TestComputeSeed_AllZeros()
    {
        var signature = new CardSignature();
        var seed = SignatureCardHelper.ComputeSeed(signature);
        Assertions.AssertThat(seed).IsEqual(-153111983);
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
    public void TestDetermineRarity_Param(float[] elements, CardRarity expectedRarity)
    {
        var signature = new CardSignature(elements);
        var rarity = SignatureCardHelper.DetermineRarity(new []{signature});
        Assertions.AssertThat(rarity).IsEqual(expectedRarity);
    }
}