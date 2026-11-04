using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>
///     The level lock on <see cref="OrderingService.Order" />: a locked item is refused before anything is
///     charged or spawned, and the same order goes through once the shop reaches the item's level.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OrderingServiceLockTest
{
    private const int StartingBalance = 300;
    private const int ItemPrice = 100;
    private const int LevelTwoXp = 500; // a 500 checkout earns 50 XP, which is level 2

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private OrderingService _ordering = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        var marker = new DeliveryMarker();
        _world.AddChild(marker);
        AddNode(_world);

        _money = new MoneyService { StartingBalance = StartingBalance };
        _progression = new ShopProgression();
        _ordering = new OrderingService
            { DeliveryPoint = marker, SpawnRoot = _world, Money = _money, Progression = _progression };
        _world.AddChild(_money);
        _world.AddChild(_progression);
        _world.AddChild(_ordering);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LockedItemIsRefusedBeforeChargingOrSpawning()
    {
        var childrenBefore = _world.GetChildCount();

        var result = _ordering.Order(MakeItem("bulk_packing_station", ItemPrice));

        AssertThat(result.Status).IsEqual(OrderStatus.Locked);
        AssertBool(result.Succeeded).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
        AssertThat(_world.GetChildCount()).IsEqual(childrenBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ReachingLevelTwoAllowsTheSameOrder()
    {
        var item = MakeItem("bulk_packing_station", ItemPrice);
        _progression.AwardCheckout(LevelTwoXp);

        var result = _ordering.Order(item);

        AssertThat(result.Status).IsEqual(OrderStatus.Success);
        AssertThat(_money.Balance).IsEqual(StartingBalance - ItemPrice);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void UnknownIdIsLockedEvenAtMaxLevel()
    {
        _progression.AwardCheckout(5000);

        var result = _ordering.Order(MakeItem("mystery", ItemPrice));

        AssertThat(result.Status).IsEqual(OrderStatus.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NoProgressionServiceFailsClosed()
    {
        _ordering.Progression = null;

        var result = _ordering.Order(MakeItem("shelf", ItemPrice));

        AssertThat(result.Status).IsEqual(OrderStatus.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LockedIsReportedBeforeAnUnaffordablePrice()
    {
        var result = _ordering.Order(MakeItem("bulk_packing_station", 5000));

        AssertThat(result.Status).IsEqual(OrderStatus.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    private static OrderItem MakeItem(string id, int price)
    {
        var root = new Node3D { Name = "Crate" };
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();
        return new OrderItem { Id = id, DisplayName = id, Price = price, Scene = packed };
    }
}
