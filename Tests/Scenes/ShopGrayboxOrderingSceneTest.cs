using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxOrderingSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";
    private static readonly string[] ExpectedItems = ["shelf", "bulk_packing_station", "cardboard_box"];

    private Node3D _shop = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _terminal = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        _ui = _shop.GetNode<OrderTerminalUi>("OrderUi");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        var ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(ordering);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown() => ServiceLocator.ResetForTesting();

    [TestCase]
    [TestCategory("Unit")]
    public void BackofficePcUsesItsDataDrivenCatalogAndShowsBalance()
    {
        AssertThat(_terminal.Catalog!.Items.Select(item => item.Id).ToArray()).IsEqual(ExpectedItems);
        AssertBool(_terminal.Catalog.Items.All(item => item.Price > 0 && item.Scene != null)).IsTrue();
        AssertThat(_ui.ItemRowCount).IsEqual(ExpectedItems.Length);
        AssertThat(_ui.BalanceText).IsEqual($"Balance: {_money.Balance}");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AffordableOrderDeductsAndSpawnsAtDeliveryPoint()
    {
        var item = _terminal.Catalog!.Items[0];
        var delivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint").GlobalPosition;
        var balance = _money.Balance;
        var children = _shop.GetNode("World").GetChildCount();

        _terminal.Interact();
        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(balance - item.Price);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(children + 1);
        var spawned = (Node3D)_shop.GetNode("World").GetChildren().Last();
        AssertBool(spawned.GlobalPosition.IsEqualApprox(delivery + OrderingService.SlotOffset(0))).IsTrue();
        AssertBool(_ui.MessageText.Contains("delivery point")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnaffordableOrderDoesNotChangeBalanceOrSpawnAnItem()
    {
        _money.TrySpend(_money.Balance);
        var children = _shop.GetNode("World").GetChildCount();

        _terminal.Interact();
        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(0);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(children);
        AssertBool(_ui.MessageText.Contains("Not enough money")).IsTrue();
    }
}
