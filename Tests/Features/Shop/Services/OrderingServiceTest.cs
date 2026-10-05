using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class OrderingServiceTest
{
    private const int StartingBalance = 300;
    private static readonly Vector3 DeliveryPosition = new(4f, 0f, -5f);

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private OrderItem _crate = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        var marker = new Marker3D { Position = DeliveryPosition };
        _world.AddChild(marker);
        AddNode(_world);

        _money = new MoneyService { StartingBalance = StartingBalance };
        _ordering = new OrderingService { DeliveryPoint = marker, SpawnRoot = _world, Money = _money };
        _world.AddChild(_money);
        _world.AddChild(_ordering);

        _crate = MakeItem("crate", 100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AffordableOrderDeductsThePriceAndSpawnsAtTheDeliveryPoint()
    {
        var result = _ordering.Order(_crate);

        AssertBool(result.Succeeded).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance - 100);
        AssertThat(result.Spawned).IsNotNull();
        AssertThat(result.Spawned!.GetParent()).IsEqual(_world);
        AssertBool(result.Spawned.GlobalPosition.IsEqualApprox(DeliveryPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnaffordableOrderSpawnsNothingAndChargesNothing()
    {
        var result = _ordering.Order(MakeItem("gold", StartingBalance + 1));

        AssertThat(result.Status).IsEqual(OrderStatus.InsufficientFunds);
        AssertBool(result.Succeeded).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(SpawnedItems().Count).IsEqual(0);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SceneWithoutA3DRootIsRejectedWithoutCharging()
    {
        var root = new Node { Name = "NotSpatial" };
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();

        var result = _ordering.Order(new OrderItem { Id = "flat", Price = 10, Scene = packed });

        AssertThat(result.Status).IsEqual(OrderStatus.InvalidItem);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ItemWithoutASceneIsRejectedWithoutCharging()
    {
        var result = _ordering.Order(new OrderItem { Id = "empty", Price = 10 });

        AssertThat(result.Status).IsEqual(OrderStatus.InvalidItem);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingDeliveryPointIsRejectedWithoutCharging()
    {
        _ordering.DeliveryPoint = null;

        var result = _ordering.Order(_crate);

        AssertThat(result.Status).IsEqual(OrderStatus.NoDeliveryPoint);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderSpawnsTheScenePackedInTheCatalogItem()
    {
        var root = new Node3D { Name = "FromCatalog" };
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();
        var item = new OrderItem { Id = "named", Price = 10, Scene = packed };

        var result = _ordering.Order(item);

        AssertThat(result.Spawned!.Name.ToString()).IsEqual("FromCatalog");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RepeatedOrdersChargeAndSpawnOncePerOrderAtDistinctSlots()
    {
        var first = _ordering.Order(_crate);
        var second = _ordering.Order(_crate);

        AssertThat(_money.Balance).IsEqual(StartingBalance - 200);
        AssertThat(SpawnedItems().Count).IsEqual(2);
        AssertBool(first.Spawned!.GlobalPosition.IsEqualApprox(second.Spawned!.GlobalPosition)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderStopsChargingOnceFundsRunOut()
    {
        _ordering.Order(_crate);
        _ordering.Order(_crate);
        _ordering.Order(_crate);
        var fourth = _ordering.Order(_crate);

        AssertThat(fourth.Status).IsEqual(OrderStatus.InsufficientFunds);
        AssertThat(_money.Balance).IsEqual(0);
        AssertThat(SpawnedItems().Count).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ManyOrdersNeverSpawnItemsOverlappingEachOther()
    {
        _money.Add(10000);
        var cheap = MakeItem("cheap", 1);
        var positions = new List<Vector3>();
        for (var order = 0; order < 3 * OrderingService.SlotColumns * OrderingService.SlotRows; order++)
        {
            var result = _ordering.Order(cheap);
            AssertBool(result.Succeeded).IsTrue();
            positions.Add(result.Spawned!.GlobalPosition);
        }

        for (var i = 0; i < positions.Count; i++)
        for (var j = i + 1; j < positions.Count; j++)
        {
            var apart = positions[i] - positions[j];
            var separated = Mathf.Abs(apart.X) >= OrderingService.SlotSpacingX - 0.001f
                            || Mathf.Abs(apart.Z) >= OrderingService.SlotSpacingZ - 0.001f
                            || Mathf.Abs(apart.Y) >= OrderingService.LayerHeight - 0.001f;
            AssertBool(separated).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FirstLayerIsCentredOnTheDeliveryPointAtGroundLevelAndLaterLayersStackAbove()
    {
        _money.Add(10000);
        var cheap = MakeItem("cheap", 1);
        var perLayer = OrderingService.SlotColumns * OrderingService.SlotRows;
        var spawned = Enumerable.Range(0, perLayer + 1).Select(_ => _ordering.Order(cheap).Spawned!.GlobalPosition)
            .ToList();
        var firstLayer = spawned.Take(perLayer).ToList();

        AssertBool(firstLayer.All(p => Mathf.IsEqualApprox(p.Y, DeliveryPosition.Y))).IsTrue();
        AssertBool(Mathf.IsEqualApprox(firstLayer.Average(p => p.X), DeliveryPosition.X)).IsTrue();
        AssertBool(Mathf.IsEqualApprox(firstLayer.Average(p => p.Z), DeliveryPosition.Z)).IsTrue();
        AssertBool(spawned[perLayer].Y > DeliveryPosition.Y).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExplicitDeliveryPointOverridesTheDefaultAndKeepsItsOwnSlotCount()
    {
        var other = new Marker3D { Position = new Vector3(-20f, 0f, 3f) };
        _world.AddChild(other);
        _money.Add(StartingBalance);

        var first = _ordering.Order(_crate);
        var elsewhere = _ordering.Order(_crate, other);
        var second = _ordering.Order(_crate);
        var elsewhereAgain = _ordering.Order(_crate, other);

        AssertBool(elsewhere.Spawned!.GlobalPosition.IsEqualApprox(other.GlobalPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
        AssertBool(elsewhereAgain.Spawned!.GlobalPosition.IsEqualApprox(other.GlobalPosition + OrderingService.SlotOffset(1)))
            .IsTrue();
        AssertBool(first.Spawned!.GlobalPosition.IsEqualApprox(DeliveryPosition + OrderingService.SlotOffset(0))).IsTrue();
        AssertBool(second.Spawned!.GlobalPosition.IsEqualApprox(DeliveryPosition + OrderingService.SlotOffset(1))).IsTrue();
        AssertThat(_ordering.DeliveredCount).IsEqual(4);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DeliveryMarkerWithALayerCapRejectsTheNextOrderWithoutChargingOnceFull()
    {
        _money.Add(10000);
        var capped = new DeliveryMarker { Position = new Vector3(-20f, 0f, 3f), MaxLayers = 1 };
        _world.AddChild(capped);
        var cheap = MakeItem("cheap", 1);
        for (var order = 0; order < OrderingService.SlotColumns * OrderingService.SlotRows; order++)
            AssertBool(_ordering.Order(cheap, capped).Succeeded).IsTrue();
        var balance = _money.Balance;
        var spawned = SpawnedItems().Count;

        var result = _ordering.Order(cheap, capped);

        AssertThat(result.Status).IsEqual(OrderStatus.DeliveryFull);
        AssertThat(_money.Balance).IsEqual(balance);
        AssertThat(SpawnedItems().Count).IsEqual(spawned);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FullCappedMarkerDoesNotBlockTheDefaultDeliveryPoint()
    {
        _money.Add(10000);
        var capped = new DeliveryMarker { Position = new Vector3(-20f, 0f, 3f), MaxLayers = 1 };
        _world.AddChild(capped);
        var cheap = MakeItem("cheap", 1);
        for (var order = 0; order <= OrderingService.SlotColumns * OrderingService.SlotRows; order++)
            _ordering.Order(cheap, capped);

        AssertBool(_ordering.Order(cheap).Succeeded).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MarkerWithoutALayerCapNeverReportsFull()
    {
        _money.Add(10000);
        var uncapped = new DeliveryMarker { Position = new Vector3(-20f, 0f, 3f) };
        _world.AddChild(uncapped);
        var cheap = MakeItem("cheap", 1);

        for (var order = 0; order < 4 * OrderingService.SlotColumns * OrderingService.SlotRows; order++)
            AssertBool(_ordering.Order(cheap, uncapped).Succeeded).IsTrue();
    }

    private List<Node3D> SpawnedItems() =>
        _world.GetChildren().OfType<Node3D>().Where(n => n is not Marker3D).ToList();

    private static OrderItem MakeItem(string id, int price)
    {
        var root = new Node3D { Name = "Crate" };
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();
        return new OrderItem { Id = id, DisplayName = id, Price = price, Scene = packed };
    }
}
