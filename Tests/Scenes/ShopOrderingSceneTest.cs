using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;
using CardCleaner.Tests.TestUtilities;

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
    // Only bounds an order loop that never fills; boxes stack in layers, so far more than one grid of them fit.
    private const int OrderBound = 200;
    private const float BoxHeight = 0.35f;
    private const float HeightTolerance = 0.001f;
    private static readonly string[] ExpectedItems =
        ["cardboard_box", "pack", "booster"];

    private Node3D _shop = null!;
    private PlayerController _player = null!;
    private ScreenTerminal _terminal = null!;
    private StockApp _stock = null!;
    private StockPresses _presses = null!;
    private PcDesktop _desktop = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private CursorOwner _cursor = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        _desktop = _shop.GetNode<PcDesktop>("PcDesktopViewport/PcDesktop");
        _stock = _shop.GetNode<StockApp>("PcDesktopViewport/StockApp");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _cursor = _shop.GetNode<CursorOwner>("Services/CursorOwner");
        var ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(_money);
        TestServices.Container.RegisterSingleton<IShopProgression>(_progression);
        TestServices.Container.RegisterSingleton<IOrderingService>(ordering);
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

        _presses = new StockPresses(_stock, ordering, _shop.GetNode("World"), OrderBound);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalCatalogOffersTheStockItems()
    {
        AssertThat(_stock.Catalog!.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedItems);
        AssertThat(_stock.CardGrid.Count).IsEqual(ExpectedItems.Length);
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
        var pcLocation = _shop.GetNode<Marker3D>("World/Markers/PcLocation").GlobalPosition;
        AssertBool(_terminal.GlobalPosition.IsEqualApprox(pcLocation)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OpeningThePcFreezesThePlayerAndClosingItRestoresControl()
    {
        _terminal.Interact();

        // The PC opens on its desktop; the ordering app only shows after the Stock icon is pressed.
        AssertBool(_desktop.Visible).IsTrue();
        AssertBool(_stock.Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsFalse();

        _terminal.Close();

        AssertBool(_desktop.Visible).IsFalse();
        AssertBool(_stock.Visible).IsFalse();
        AssertBool(_player.ControlEnabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PcOpenThenClose_LeavesCursorCaptured()
    {
        _terminal.Interact();

        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Visible);

        _terminal.Close();

        AssertThat(_cursor.AppliedMode).IsEqual(Input.MouseModeEnum.Captured);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemovingTheOpenPcFromTheSceneStillRestoresPlayerControl()
    {
        _terminal.Interact();

        _terminal.GetParent().RemoveChild(_terminal);

        AssertBool(_player.ControlEnabled).IsTrue();
        _terminal.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerStaysPutWhileThePcIsOpenAndWalksAgainAfterClosing()
    {
        for (var frame = 0; frame < 30; frame++)
            await ISceneRunner.SyncPhysicsFrame;
        var start = _player.GlobalPosition;
        Vector3 whileOpen;
        Vector3 afterClose;
        Input.ActionPress("ui_up");
        try
        {
            _terminal.Interact();

            for (var frame = 0; frame < 20; frame++)
                await ISceneRunner.SyncPhysicsFrame;
            whileOpen = _player.GlobalPosition;
            _terminal.Close();
            for (var frame = 0; frame < 20; frame++)
                await ISceneRunner.SyncPhysicsFrame;
            afterClose = _player.GlobalPosition;
        }
        finally
        {
            Input.ActionRelease("ui_up");
        }

        AssertBool(new Vector2(whileOpen.X - start.X, whileOpen.Z - start.Z).Length() < 0.01f).IsTrue();
        AssertBool(new Vector2(afterClose.X - whileOpen.X, afterClose.Z - whileOpen.Z).Length() > 0.1f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingEachItemChargesTheBalanceAndDeliversItToTheStockDeliveryArea()
    {
        var delivery = StockDelivery;
        var balance = _money.Balance;
        _terminal.Interact();

        for (var i = 0; i < ExpectedItems.Length; i++)
        {
            var item = _stock.Catalog!.Items[i];
            _money.Add(item.Price);
            balance = _money.Balance;

            var spawned = _presses.PressForDelivery(i);

            AssertThat(_money.Balance).IsEqual(balance - item.Price);
            AssertThat(_stock.BalanceText).IsEqual($"Balance: {_money.Balance}");
            AssertBool(spawned.GlobalPosition.IsEqualApprox(delivery.GlobalPosition + delivery.SlotOffset(i)))
                .IsTrue();
        }
    }

    // The furniture area takes shelf-sized cells; the stock area takes cells that fit the 0.61 x 0.83 m cardboard box.
    [TestCase("World/Markers/DeliveryPoint", 2.13f, 1.0f)]
    [TestCase("World/Markers/StockDeliveryPoint", 0.61f, 0.83f)]
    [TestCategory("Unit")]
    public void FirstLayerOfDeliverySlotsFitsInsideTheRightStorageFloor(string markerPath, float widestItem,
        float deepestItem)
    {
        var storage = _shop.GetNode<StaticBody3D>("World/RightStorage/Floor");
        var shape = (BoxShape3D)storage.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var delivery = _shop.GetNode<DeliveryMarker>(markerPath);

        for (var i = 0; i < delivery.SlotCount; i++)
        {
            var local = delivery.GlobalPosition + delivery.SlotOffset(i) - storage.GlobalPosition;
            AssertBool(Mathf.Abs(local.X) + widestItem / 2f < shape.Size.X / 2f).IsTrue();
            AssertBool(Mathf.Abs(local.Z) + deepestItem / 2f < shape.Size.Z / 2f).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BoxesFillTheGridAndStackOnTheBoxBelow()
    {
        var delivery = StockDelivery;
        var box = _stock.Catalog!.Items.ToList().FindIndex(item => item.Id == "cardboard_box");
        _money.Add(_stock.Catalog.Items[box].Price * OrderBound);
        _terminal.Interact();

        var delivered = _presses.OrderUntilFull(box);

        AssertBool(_stock.CartPanel.MessageText.Contains("delivery area is full")).IsTrue();
        var perLayer = delivery.SlotCount;
        AssertThat(delivered.Count).IsGreater(perLayer);
        // The first box of the second layer rests on the box below it.
        AssertFloat(delivered[perLayer].GlobalPosition.Y - delivery.GlobalPosition.Y)
            .IsEqualApprox(BoxHeight + DeliveryMarker.StackGap, HeightTolerance);
    }

    // In play, six small stock orders took all six shelf-sized cells of the one delivery area, and the next furniture
    // order was refused as "The delivery area is full". Stock now has an area of its own.
    [TestCase]
    [TestCategory("Unit")]
    public void StockOrdersLeaveTheFurnitureDeliveryAreaFree()
    {
        var furniture = _shop.GetNode<StockApp>("PcDesktopViewport/FurnitureApp");
        var ordering = _shop.GetNode<OrderingService>("Services/OrderingService");
        var furniturePresses = new StockPresses(furniture, ordering, _shop.GetNode("World"), OrderBound);
        var furnitureDelivery = _shop.GetNode<DeliveryMarker>("World/Markers/DeliveryPoint");
        var booster = _stock.Catalog!.Items.ToList().FindIndex(item => item.Id == "booster");
        var shelf = furniture.Catalog!.Items.ToList().FindIndex(item => item.Id == "shelf_unit");
        _money.Add(10000);
        _terminal.Interact();

        for (var order = 0; order < furnitureDelivery.SlotCount; order++)
            _presses.PressForDelivery(booster);
        var shelfBox = furniturePresses.PressForDelivery(shelf);

        AssertBool(shelfBox.GlobalPosition.IsEqualApprox(
            furnitureDelivery.GlobalPosition + furnitureDelivery.SlotOffset(0))).IsTrue();
    }

    private DeliveryMarker StockDelivery => _shop.GetNode<DeliveryMarker>("World/Markers/StockDeliveryPoint");

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingBeyondTheBalanceShowsAMessageAndSpawnsNothing()
    {
        var items = _shop.GetNode("World").GetChildCount();
        _terminal.Interact();
        _money.TrySpend(_money.Balance);

        _presses.PressForRefusal(0, "Not enough money");

        AssertThat(_money.Balance).IsEqual(0);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(items);
    }
}
