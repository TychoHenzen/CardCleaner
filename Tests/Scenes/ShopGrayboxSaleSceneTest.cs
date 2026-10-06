using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxSaleSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";

    private Node3D _shop = null!;
    private CardShelf _shelf = null!;
    private SaleRegister _register = null!;
    private MoneyService _money = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _shelf = _shop.GetNode<CardShelf>("World/Storage/Shelves");
        _register = _shop.GetNode<SaleRegister>("World/Storefront/CheckoutCounter");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_register))
            _register.Money = null;
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();

        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GrayboxWiresTheShelfAndRegisterToTheSharedBalance()
    {
        AssertThat(_shelf.SlotCount).IsGreater(0);
        AssertThat(_register.PlayerHolder).IsNotNull();
        AssertThat(_register.StatusLabel!.Text).Contains($"Balance: {_money.Balance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task StockedShelfCardSellsForTheFixedPriceAndLeavesTheSlotEmpty()
    {
        var card = ShopTestCards.Create(new CardSignature());
        _shop.GetNode("World/Cards").AddChild(card);
        _shelf.Slots.First().Area.EmitSignal(Area3D.SignalName.BodyEntered, card);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        var balance = _money.Balance;
        _register.Interact();

        AssertThat(_money.Balance).IsEqual(balance + CardPricing.FixedCardPrice);
        AssertThat(_shelf.StockedCount).IsEqual(0);
        AssertBool(card.IsQueuedForDeletion()).IsTrue();
        AssertThat(_register.StatusLabel!.Text).Contains($"Balance: {_money.Balance}");
    }
}
