// File: SignatureCardHelperTest.cs

using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using GdUnit4;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features;

[TestSuite]
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
    [TestCase(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, CardRarity.Common)]
    [TestCase(-0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, CardRarity.Common)]
    [TestCase(0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, 0.45f, CardRarity.Common)]
    [TestCase(-0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, -0.45f, CardRarity.Common)]
    [TestCase(0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, 0.55f, CardRarity.Common)]

    // Uncommon (0.4458 <= ratio < 0.58996)
    [TestCase(0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, 0.8f, CardRarity.Uncommon)]
    [TestCase(-0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, -0.8f, CardRarity.Uncommon)]
    [TestCase(0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, 0.2f, CardRarity.Uncommon)]
    [TestCase(-0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, -0.2f, CardRarity.Uncommon)]
    [TestCase(0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.2f, CardRarity.Uncommon)]

    // Rare (0.58996 <= ratio < 0.81307)
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f, CardRarity.Rare)]
    [TestCase(1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, CardRarity.Rare)]
    [TestCase(1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, CardRarity.Rare)]
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, 0.85f, CardRarity.Rare)]
    [TestCase(-1.0f, -1.0f, -1.0f, -1.0f, -0.15f, -0.15f, -0.15f, -0.15f, CardRarity.Rare)]

    // Epic (0.81307 <= ratio < 0.90223)
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f, CardRarity.Epic)]
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.85f, CardRarity.Epic)]
    [TestCase(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, 0.5f, CardRarity.Epic)]
    [TestCase(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -0.85f, -0.85f, CardRarity.Epic)]
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, 0.75f, CardRarity.Epic)]

    // Legendary (ratio >= 0.90223)
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, CardRarity.Legendary)]
    [TestCase(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, CardRarity.Legendary)]
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.85f, CardRarity.Legendary)]
    [TestCase(1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.75f, CardRarity.Legendary)]
    [TestCase(-1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -1.0f, -0.75f, CardRarity.Legendary)]
    public void TestDetermineRarity_Param(
        float e0, float e1, float e2, float e3,
        float e4, float e5, float e6, float e7,
        CardRarity expect)
    {
        var signature = new CardSignature(new[] { e0, e1, e2, e3, e4, e5, e6, e7 });
        var rarity = SignatureCardHelper.DetermineRarity(signature);
        Assertions.AssertThat(rarity).IsEqual(expect);
    }
}