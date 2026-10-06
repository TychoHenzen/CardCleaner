using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxWorkshopOrderingSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";
    private static readonly string[] ExpectedItems = ["conveyor", "arcade_cabinet", "wiring"];

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private PlayerController _player = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _workshop = _shop.GetNode<Node3D>("World/PortalWorkshop");
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _workshop.GetNode<OrderTerminal>("OrderingRoom/OrderTerminal");
        _ui = _workshop.GetNode<OrderTerminalUi>("OrderUi");
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
    public void Teardown()
    {
        if (GodotObject.IsInstanceValid(_terminal))
            _terminal.Close();
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();

        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopTerminalUsesItsOwnCatalogAndSharesTheShopBalance()
    {
        var pc = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");

        AssertThat(_terminal.Catalog!.Items.Select(item => item.Id).ToArray()).IsEqual(ExpectedItems);
        AssertThat(_ui.ItemRowCount).IsEqual(ExpectedItems.Length);
        AssertThat(_ui.BalanceText).IsEqual($"Balance: {_money.Balance}");
        AssertBool(_terminal.Catalog.Items.Select(item => item.Id)
            .Intersect(pc.Catalog!.Items.Select(item => item.Id)).Any()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerCanReachTheWorkshopTerminalAndOpenItsUi()
    {
        var interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = _terminal.GlobalPosition + new Vector3(0f, 1f, 2f);
        var to = _terminal.GlobalPosition + new Vector3(0f, 1f, 0f);
        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from,
            To = to,
            CollideWithBodies = true,
            CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_terminal);
        AssertBool(from.DistanceTo(_terminal.GlobalPosition) <= _terminal.InteractionRange).IsTrue();

        _terminal.Interact();

        AssertBool(_ui.Visible).IsTrue();
        AssertBool(_shop.GetNode<OrderTerminalUi>("OrderUi").Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopOrderDeductsMoneyAndSpawnsAtWorkshopDeliveryPoint()
    {
        var item = _terminal.Catalog!.Items[0];
        var delivery = _workshop.GetNode<Marker3D>("DeliveryPoint");
        var world = _shop.GetNode("World");
        var balance = _money.Balance;
        var children = world.GetChildCount();

        _terminal.Interact();
        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(balance - item.Price);
        AssertThat(world.GetChildCount()).IsEqual(children + 1);
        var spawned = (Node3D)world.GetChildren().Last();
        AssertBool(spawned.GlobalPosition.IsEqualApprox(delivery.GlobalPosition + OrderingService.SlotOffset(0))).IsTrue();
        AssertBool(_ui.MessageText.Contains("delivery point")).IsTrue();
    }
}
