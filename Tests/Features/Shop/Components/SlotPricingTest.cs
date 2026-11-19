using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     A shelf price tag prints the asking price of its stock and opens the pricing screen for it. Every item carries
///     its own price, cards and containers alike, so a price set on one slot's boosters changes no other booster.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SlotPricingTest
{
    private const string CardSlotScene = "res://Scenes/UI/CardSlot.tscn";
    private const string BoosterScene = "res://Scenes/Shop/Items/Booster.tscn";
    private const string BoxScene = "res://Scenes/Shop/Items/CardboardBox.tscn";
    private const string WholesaleCatalog = "res://Data/Shop/BackofficeCatalog.tres";

    // What a booster costs to order in the backoffice catalog.
    private const int BoosterWholesale = 2;

    private DeckSlot _slot = null!;
    private Label3D _label = null!;
    private SlotPricing _pricing = null!;
    private WholesaleCosts _costs = null!;
    private RecordingPricingScreen _screen = null!;

    [BeforeTest]
    public async Task Setup()
    {
        TestServices.Reset();
        _costs = AddNode(new WholesaleCosts { Wholesale = GD.Load<OrderCatalog>(WholesaleCatalog) });
        _screen = new RecordingPricingScreen();
        TestServices.Container.RegisterSingleton<IWholesaleCosts>(_costs);
        TestServices.Container.RegisterSingleton<IPricingScreen>(_screen);

        AddNode(new Node3D());
        _slot = AddNode(GD.Load<PackedScene>(CardSlotScene).Instantiate<DeckSlot>());
        _label = AddNode(new Label3D());
        _pricing = AddNode(new SlotPricing { Slot = _slot, TagLabel = _label });
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown() => TestServices.Reset();

    [TestCase]
    public void AnEmptySlotShowsNoTagAndCannotBeInteracted()
    {
        _pricing.Interact();

        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
        AssertThat(_pricing.AskingValue).IsEqual(0);
        AssertObject(_screen.Opened).IsNull();
    }

    [TestCase]
    public void AStockedCardAsksItsMarketPrice()
    {
        _slot.TryLock(AddNode(ShopTestCards.Create(new CardSignature())));

        AssertThat(_label.Text).IsEqual($"{CardValue.MinimumCardPrice}");
        AssertThat(_pricing.AskingValue).IsEqual(CardValue.MinimumCardPrice);
        AssertThat(_pricing.Prompt).IsEqual($"Set price (now {CardValue.MinimumCardPrice})");
    }

    [TestCase]
    public void ACardAsksThePriceSetOnIt()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        card.ShopPrice = 5;

        _slot.TryLock(card);

        AssertThat(_label.Text).IsEqual("5");
        AssertThat(_pricing.AskingValue).IsEqual(5);
    }

    [TestCase]
    public void InteractOpensTheScreenForTheCardAndANewPriceRepaintsTheTag()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(card);

        _pricing.Interact();

        var stock = _screen.Opened!;
        AssertThat(stock.ItemName).IsEqual("Common card");
        AssertThat(stock.MarketValue).IsEqual(CardValue.MinimumCardPrice);
        AssertThat(stock.Price).IsEqual(CardValue.MinimumCardPrice);
        AssertBool(stock.UnitCost.HasValue).IsFalse();

        stock.SetPrice(7);

        AssertBool(card.ShopPrice == 7).IsTrue();
        AssertThat(_label.Text).IsEqual("7");
    }

    [TestCase]
    public void ThePriceStaysWithTheCardNotTheSlot()
    {
        var priced = AddNode(ShopTestCards.Create(new CardSignature()));
        var plain = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(priced);
        _pricing.PricedStock()!.SetPrice(7);

        _slot.ReleaseCard(priced);
        _slot.TryLock(plain);

        AssertThat(_label.Text).IsEqual($"{CardValue.MinimumCardPrice}");
        AssertBool(priced.ShopPrice == 7).IsTrue();
    }

    [TestCase]
    public void TheTagFollowsTheSlotWhenTheStockLeaves()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature()));
        _slot.TryLock(card);

        _slot.ReleaseCard(card);

        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
    }

    [TestCase]
    public void TheValueReadsTheCurrentStock()
    {
        var card = AddNode(ShopTestCards.Create(new CardSignature().With(Element.Lumines, 0.4f)));
        _slot.TryLock(card);

        AssertBool(_pricing.MarketValue >= CardValue.SpecialMultiplier * CardValue.MinimumCardPrice).IsTrue();
    }

    [TestCase]
    public void ABoosterAsksItsMarketPriceUntilAPriceIsSetOnIt()
    {
        StockBooster();
        AssertThat(_label.Text).IsEqual($"{ProductMarketPrices.BoosterPrice}");

        var priced = AddNode(NewBooster());
        priced.ShopPrice = 6;
        AssertBool(_slot.TryLock(priced)).IsTrue();

        // The tag prints the price of the booster a customer takes next, the top of the stack.
        AssertThat(_label.Text).IsEqual("6");
        AssertThat(_pricing.AskingValue).IsEqual(6);
    }

    [TestCase]
    public void InteractOnABoosterStackPricesEveryBoosterInTheSlotOnly()
    {
        var first = StockBooster();
        var second = StockBooster();
        var elsewhere = AddNode(NewBooster());

        _pricing.Interact();

        var stock = _screen.Opened!;
        AssertThat(stock.ItemName).IsEqual("Booster pack");
        AssertThat(stock.Scope).Contains("2 booster packs");
        AssertThat(stock.MarketValue).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertBool(stock.UnitCost == BoosterWholesale).IsTrue();

        stock.SetPrice(5);

        AssertBool(first.ShopPrice == 5).IsTrue();
        AssertBool(second.ShopPrice == 5).IsTrue();
        AssertBool(elsewhere.ShopPrice.HasValue).IsFalse();
        AssertThat(_label.Text).IsEqual("5");
    }

    [TestCase]
    public void ThePriceStaysWithTheBoosterNotTheSlot()
    {
        var priced = StockBooster();
        _pricing.PricedStock()!.SetPrice(7);

        _slot.ReleaseCard(priced);
        StockBooster();

        AssertThat(_label.Text).IsEqual($"{ProductMarketPrices.BoosterPrice}");
        AssertBool(priced.ShopPrice == 7).IsTrue();
    }

    [TestCase]
    public void ABoxShowsNoTagAndOpensNoScreen()
    {
        _slot.Kind = ShelfKind.StorageRack;
        var box = AddNode(GD.Load<PackedScene>(BoxScene).Instantiate<CardContainer>());
        AssertBool(_slot.TryLock(box)).IsTrue();

        _pricing.Interact();

        AssertThat(_label.Text).IsEqual(string.Empty);
        AssertBool(_pricing.CanInteract).IsFalse();
        AssertThat(_pricing.AskingValue).IsEqual(0);
        AssertObject(_screen.Opened).IsNull();
    }

    private CardContainer StockBooster()
    {
        _slot.Kind = ShelfKind.ProductShelf;
        var booster = AddNode(NewBooster());
        AssertBool(_slot.TryLock(booster)).IsTrue();
        return booster;
    }

    private static CardContainer NewBooster() => GD.Load<PackedScene>(BoosterScene).Instantiate<CardContainer>();

    private sealed class RecordingPricingScreen : IPricingScreen
    {
        public IPricedStock? Opened { get; private set; }

        public bool IsOpen => Opened != null;

        public void Open(IPricedStock stock) => Opened = stock;
    }
}
