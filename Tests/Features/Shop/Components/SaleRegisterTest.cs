using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Mocking;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class SaleRegisterTest
{
    private const int StartingBalance = 100;
    private const string ShelfScene = "res://Scenes/Shop/Items/Shelf.tscn";

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private Label3D _label = null!;
    private SaleRegister _register = null!;
    private CardShelf _shelf = null!;
    private CardHolder _holder = null!;

    [BeforeTest]
    public async Task Setup()
    {
        ServiceLocator.ResetForTesting();
        _world = new Node3D();
        _money = new MoneyService { StartingBalance = StartingBalance };
        _label = new Label3D();
        _register = new SaleRegister { StatusLabel = _label };
        _shelf = GD.Load<PackedScene>(ShelfScene).Instantiate<CardShelf>();
        var hand = new Node3D { Name = "Hand" };
        _holder = new CardHolder();
        _holder.SetReferences(hand);
        var spawner = new MockCardSpawner();
        ServiceLocator.Container.RegisterSingleton<ICardSpawner>(spawner);
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);

        foreach (var node in new Node[] { _money, spawner, hand, _holder, _label, _register, _shelf })
            _world.AddChild(node);
        _register.PlayerHolder = _holder;
        AddNode(_world);
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LabelShowsTheBalanceFromTheStart()
    {
        AssertThat(_label.Text).Contains($"Balance: {StartingBalance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task InteractingSellsAShelfCardForTheFixedPrice()
    {
        var card = await Stock(new CardSignature());

        _register.Interact();

        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
        AssertBool(card.IsQueuedForDeletion()).IsTrue();
        AssertThat(_shelf.StockedCount).IsEqual(0);
        AssertThat(_label.Text).Contains($"Balance: {_money.Balance}");
        AssertThat(_label.Text).Contains($"Sold for {CardPricing.FixedCardPrice}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractingSellsTheHeldCardFirst()
    {
        var card = MakeCard(new CardSignature());
        _holder.AddCard(card);

        _register.Interact();

        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
        AssertBool(card.IsQueuedForDeletion()).IsTrue();
        AssertThat(_holder.HeldCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SellingWithNothingToSellKeepsTheBalance()
    {
        var result = _register.Sell();

        AssertThat(result.Status).IsEqual(SaleStatus.NothingToSell);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_label.Text).Contains("Nothing to sell");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ASpecialShelfCardSellsForTheSameFixedPrice()
    {
        await Stock(new CardSignature { Lumines = 0.4f });

        var result = _register.Sell();

        AssertThat(result.Price).IsEqual(CardPricing.FixedCardPrice);
        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACardThatCannotBeSoldStaysOnTheShelfAndDoesNotBlockTheNext()
    {
        var broken = await Stock(null, 0);
        var good = await Stock(new CardSignature(), 1);

        var result = _register.Sell();

        AssertThat(result.Status).IsEqual(SaleStatus.Sold);
        AssertBool(good.IsQueuedForDeletion()).IsTrue();
        AssertBool(broken.IsQueuedForDeletion()).IsFalse();
        AssertThat(_shelf.StockedCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task EachInteractionSellsExactlyOneCard()
    {
        await Stock(new CardSignature(), 0);
        await Stock(new CardSignature(), 1);

        _register.Interact();

        AssertThat(_money.Balance).IsEqual(StartingBalance + CardPricing.FixedCardPrice);
        AssertThat(_shelf.StockedCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RegisterWithoutAMoneyServiceDoesNotThrowOrPay()
    {
        var lonelyLabel = new Label3D();
        var lonely = new SaleRegister { StatusLabel = lonelyLabel };
        _world.AddChild(lonelyLabel);
        _world.AddChild(lonely);
        lonely.Money = null;

        var result = lonely.Sell();

        AssertThat(result.Status).IsEqual(SaleStatus.NothingToSell);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    private static CardController MakeCard(CardSignature? signature)
    {
        var card = ShopTestCards.Create(signature);
        AddNode(card);
        return card;
    }

    private async Task<CardController> Stock(CardSignature? signature, int slotIndex = 0)
    {
        var card = MakeCard(signature);
        var slot = _shelf.Slots.ElementAt(slotIndex);
        slot.Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        return card;
    }
}
