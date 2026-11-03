using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The terminal's answer to a level-locked item: the lock message shows, and nothing is charged or delivered.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class OrderTerminalLockTest
{
    private const int StartingBalance = 150;
    private const int CheapPrice = 100;
    private const int DearPrice = 400;
    private const string LockedId = "dear";

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private OrderTerminalUi _ui = null!;
    private OrderTerminal _terminal = null!;
    private CursorOwner _cursor = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        var marker = new DeliveryMarker();
        _world.AddChild(marker);
        _money = new MoneyService { StartingBalance = StartingBalance };
        _ordering = new OrderingService
            { DeliveryPoint = marker, SpawnRoot = _world, Money = _money, Progression = new OpenProgression(LockedId) };
        _ui = new OrderTerminalUi();
        _terminal = new OrderTerminal { Catalog = MakeCatalog(("cheap", CheapPrice), (LockedId, DearPrice)), Ui = _ui };
        _cursor = new CursorOwner();
        _world.AddChild(_money);
        _world.AddChild(_ordering);
        _world.AddChild(_ui);
        _world.AddChild(_cursor);

        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IMoneyService>(_money);
        TestServices.Container.RegisterSingleton<IOrderingService>(_ordering);
        TestServices.Container.RegisterSingleton<ICursorOwner>(_cursor);

        _world.AddChild(_terminal);
        AddNode(_world);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingALockedItemShowsTheLockMessageAndChangesNothing()
    {
        _terminal.Interact();

        _ui.PressItem(1);

        AssertThat(_terminal.LastOrder?.Status).IsEqual(OrderStatus.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_ordering.DeliveredCount).IsEqual(0);
        AssertThat(_ui.MessageText).IsEqual("dear is locked. Reach a higher shop level to order it.");
        AssertBool(_ui.MessageText.Contains("cannot be ordered right now")).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressingAnUnlockedItemStillOrders()
    {
        _terminal.Interact();

        _ui.PressItem(0);

        AssertBool(_ui.MessageText.Contains("Ordered")).IsTrue();
        AssertThat(_ordering.DeliveredCount).IsEqual(1);
    }

    private static OrderCatalog MakeCatalog(params (string Id, int Price)[] entries)
    {
        var root = new Node3D();
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();

        var items = new OrderItem[entries.Length];
        for (var i = 0; i < entries.Length; i++)
            items[i] = new OrderItem
            {
                Id = entries[i].Id, DisplayName = entries[i].Id, Price = entries[i].Price, Scene = packed
            };
        return new OrderCatalog { Items = items };
    }
}
