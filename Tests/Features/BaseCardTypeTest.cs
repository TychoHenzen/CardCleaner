using CardCleaner.Scripts.Features.Card.Models;
using GdUnit4;
using BaseCardType = CardCleaner.Scripts.Features.Card.Models.BaseCardType;
using CardSignature = CardCleaner.Scripts.Features.Card.Models.CardSignature;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class BaseCardTypeTest
{
    private BaseCardType CreateTestCardType()
    {
        var cardType = new BaseCardType();
        cardType.TypeName = "Test Card";
        cardType.BaseSignature = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        cardType.MatchRadius = 0.5f;
        cardType.BaseRarity = CardRarity.Common;
        cardType.BaseValue = 10.0f;
        cardType.BasePower = 1.0f;
        cardType.BaseCost = 1;
        return cardType;
    }

    [TestCase]
    public void TestCanMatchWithinRadius()
    {
        var cardType = CreateTestCardType();
        var closeSignature = new CardSignature(new[] { 0.7f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        Assertions.AssertBool(cardType.CanMatch(closeSignature)).IsTrue();
    }

    [TestCase]
    public void TestCannotMatchOutsideRadius()
    {
        var cardType = CreateTestCardType();
        var farSignature = new CardSignature(new[] { 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        Assertions.AssertBool(cardType.CanMatch(farSignature)).IsFalse();
    }

    [TestCase]
    public void TestMatchWeightCalculation()
    {
        var cardType = CreateTestCardType();
        var exactMatch = cardType.BaseSignature;
        var closeMatch = new CardSignature(new[] { 0.6f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var farMatch = new CardSignature(new[] { 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

        var exactWeight = cardType.CalculateMatchWeight(exactMatch);
        var closeWeight = cardType.CalculateMatchWeight(closeMatch);
        var farWeight = cardType.CalculateMatchWeight(farMatch);

        Assertions.AssertFloat(exactWeight).IsEqual(1.0f);
        Assertions.AssertThat(closeWeight).IsLess(exactWeight);
        Assertions.AssertThat(farWeight).IsEqual(0.0f); // Outside match radius
    }

    [TestCase]
    public void TestRarityCalculation()
    {
        var cardType = CreateTestCardType();
        cardType.BaseRarity = CardRarity.Common;

        // Test with signature close to base (should remain common)
        var normalSignature = new CardSignature(new[] { 0.4f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        Assertions.AssertThat(cardType.CalculateActualRarity(normalSignature)).IsEqual(CardRarity.Common);

        // Test with highly unusual signature (should increase rarity)
        var unusualSignature = new CardSignature(new[] { 1.0f, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var actualRarity = cardType.CalculateActualRarity(unusualSignature);
        Assertions.AssertThat((int)actualRarity).IsGreater((int)CardRarity.Common);
    }

    [TestCase]
    public void TestValueCalculation()
    {
        var cardType = CreateTestCardType();
        cardType.BaseValue = 10.0f;

        var signature = new CardSignature(new[] { 0.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var actualValue = cardType.CalculateActualValue(signature);

        // Should be at least base value
        Assertions.AssertThat(actualValue).IsGreaterEqual(cardType.BaseValue);
    }
}