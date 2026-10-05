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

/// <summary>
/// End to end in the shop scene: the PC is reachable by the player's interaction ray, opens the ordering
/// UI, and ordering each catalog item charges the balance and delivers a physical item to the delivery point.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopOrderingSceneTest
{
    private const uint InteractionMask = 6;
    private const float StandingDistanceToPc = 2.9f;
    private static readonly string[] ExpectedItems = ["shelf", "bulk_packing_station", "cardboard_box"];

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _shop.GetNode<OrderTerminal>("World/PcTerminal");
        _ui = _shop.GetNode<OrderTerminalUi>("OrderUi");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        ServiceLocator.Container.RegisterSingleton<IMoneyService>(_money);
        ServiceLocator.Container.RegisterSingleton<IOrderingService>(_ordering);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalCatalogHasShelfPackingStationAndCardboardBox()
    {
        AssertThat(_terminal.Catalog!.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedItems);
        AssertThat(_ui.ItemRowCount).IsEqual(ExpectedItems.Length);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerInteractionRayCanReachThePcFromInFrontOfTheDesk()
    {
        var interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = _terminal.GlobalPosition + new Vector3(0f, 1.0f, StandingDistanceToPc);
        var to = _terminal.GlobalPosition + new Vector3(0f, 1.0f, 0f);

        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = to, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(interaction.InteractableCollisionMask & InteractionMask).IsNotEqual(0u);
        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_terminal);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PcIsWithinInteractionRangeFromStandingDistance()
    {
        var eyes = _terminal.GlobalPosition + new Vector3(0f, 1.8f, StandingDistanceToPc);

        AssertBool(eyes.DistanceTo(_terminal.GlobalPosition) <= _terminal.InteractionRange).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PcStaysOutOfThePlayerRouteAndInsideTheBackoffice()
    {
        var backoffice = _shop.GetNode<StaticBody3D>("World/Backoffice/Floor");
        var shape = (BoxShape3D)backoffice.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var local = _terminal.GlobalPosition - backoffice.GlobalPosition;

        AssertBool(Mathf.Abs(local.X) < shape.Size.X / 2f).IsTrue();
        AssertBool(Mathf.Abs(local.Z) < shape.Size.Z / 2f).IsTrue();
        AssertBool(_terminal.GlobalPosition.IsEqualApprox(_shop.GetNode<Marker3D>("World/Markers/PcLocation").GlobalPosition))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpeningThePcFreezesThePlayerAndClosingItRestoresControl()
    {
        _terminal.Interact();

        AssertBool(_ui.Visible).IsTrue();
        AssertBool(_player.ControlEnabled).IsFalse();

        _terminal.Close();

        AssertBool(_ui.Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingEachItemChargesTheBalanceAndDeliversItToTheDeliveryPoint()
    {
        var delivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint").GlobalPosition;
        var balance = _money.Balance;
        _terminal.Interact();

        for (var i = 0; i < ExpectedItems.Length; i++)
        {
            var item = _terminal.Catalog!.Items[i];
            _money.Add(item.Price);
            balance = _money.Balance;
            var before = _shop.GetNode("World").GetChildCount();

            _ui.PressItem(i);

            AssertThat(_money.Balance).IsEqual(balance - item.Price);
            AssertThat(_ui.BalanceText).IsEqual($"Balance: {_money.Balance}");
            AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(before + 1);
            var spawned = (Node3D)_shop.GetNode("World").GetChildren().Last();
            AssertBool(spawned.GlobalPosition.IsEqualApprox(delivery + OrderingService.SlotOffset(i))).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FirstLayerOfDeliverySlotsFitsInsideTheStorageFloor()
    {
        var storage = _shop.GetNode<StaticBody3D>("World/Storage/Floor");
        var shape = (BoxShape3D)storage.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var delivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint").GlobalPosition;
        const float widestItem = 2.13f;
        const float deepestItem = 1.0f;
        var perLayer = OrderingService.SlotColumns * OrderingService.SlotRows;

        for (var i = 0; i < perLayer; i++)
        {
            var local = delivery + OrderingService.SlotOffset(i) - storage.GlobalPosition;
            AssertBool(Mathf.Abs(local.X) + widestItem / 2f < shape.Size.X / 2f).IsTrue();
            AssertBool(Mathf.Abs(local.Z) + deepestItem / 2f < shape.Size.Z / 2f).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingBeyondTheBalanceShowsAMessageAndSpawnsNothing()
    {
        var items = _shop.GetNode("World").GetChildCount();
        _terminal.Interact();
        _money.TrySpend(_money.Balance);

        _ui.PressItem(0);

        AssertThat(_money.Balance).IsEqual(0);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(items);
        AssertBool(_ui.MessageText.Contains("Not enough money")).IsTrue();
    }
}
