using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class CardSaleTest
{
    private const int StartingBalance = 100;

    private MoneyService _money = null!;

    [BeforeTest]
    public void Setup()
    {
        _money = new MoneyService { StartingBalance = StartingBalance };
    }

    [AfterTest]
    public void Teardown()
    {
        _money.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SellingACommonCardPaysThePriceAndRemovesTheCard()
    {
        var card = MakeCard(new CardSignature());

        var result = CardSale.TrySell(card, _money);

        AssertThat(result.Status).IsEqual(SaleStatus.Sold);
        AssertThat(result.Price).IsEqual(CardPricing.FixedCardPrice);
        AssertThat(result.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
        AssertBool(card.IsQueuedForDeletion()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReleaseRunsOnceBeforeTheCardIsFreed()
    {
        var card = MakeCard(new CardSignature());
        var released = 0;

        CardSale.TrySell(card, _money, _ => released++);

        AssertThat(released).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SellingNothingDoesNotChangeTheBalance()
    {
        var result = CardSale.TrySell(null, _money);

        AssertThat(result.Status).IsEqual(SaleStatus.NothingToSell);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SellingAFreedCardDoesNothing()
    {
        var card = MakeCard(new CardSignature());
        card.Free();

        var result = CardSale.TrySell(card, _money);

        AssertThat(result.Status).IsEqual(SaleStatus.NothingToSell);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnySignatureSellsForTheSameFixedPrice()
    {
        var card = MakeCard(new CardSignature { Febris = 0.7f });

        var result = CardSale.TrySell(card, _money);

        AssertThat(result.Status).IsEqual(SaleStatus.Sold);
        AssertThat(result.Price).IsEqual(CardPricing.FixedCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACardWithoutSignatureIsRefusedAndStaysUntouched()
    {
        var card = new CardController { Name = "Card1" };
        AddNode(card);
        var released = 0;

        var result = CardSale.TrySell(card, _money, _ => released++);

        AssertThat(result.Status).IsEqual(SaleStatus.NotSellable);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(released).IsEqual(0);
        AssertBool(card.IsQueuedForDeletion()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ABodyThatIsNotACardIsNotSellable()
    {
        var body = new RigidBody3D();
        AddNode(body);

        var result = CardSale.TrySell(body, _money);

        AssertThat(result.Status).IsEqual(SaleStatus.NotSellable);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ACardCannotBeSoldTwice()
    {
        var card = MakeCard(new CardSignature());

        var first = CardSale.TrySell(card, _money);
        var second = CardSale.TrySell(card, _money);

        AssertThat(first.Status).IsEqual(SaleStatus.Sold);
        AssertThat(second.Status).IsEqual(SaleStatus.AlreadySold);
        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
    }

    private static CardController MakeCard(CardSignature signature)
    {
        var card = new CardController { Name = "Card1", Signature = signature };
        AddNode(card);
        return card;
    }
}
