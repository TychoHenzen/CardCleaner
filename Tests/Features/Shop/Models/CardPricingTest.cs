using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CardPricingTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void CommonCardHasTheFixedPrice()
    {
        AssertThat(CardPricing.GetPrice(new CardSignature())).IsEqual(CardPricing.FixedCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryCardHasTheSameFixedPrice()
    {
        var common = CardPricing.GetPrice(new CardSignature(new float[8]));

        for (var i = 0; i < 8; i++)
            AssertThat(CardPricing.GetPrice(new CardSignature { [i] = 0.5f })).IsEqual(common);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MissingSignatureHasNoPrice()
    {
        AssertThat(CardPricing.GetPrice(null)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PricingIsDeterministic()
    {
        var signature = new CardSignature { Febris = -0.3f };

        AssertThat(CardPricing.GetPrice(signature)).IsEqual(CardPricing.GetPrice(signature));
    }
}
