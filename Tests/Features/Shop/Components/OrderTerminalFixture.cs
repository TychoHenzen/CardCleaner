using Godot;
using CardCleaner.Tests.TestUtilities;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Pause.Models;
using CardCleaner.Scripts.Features.Pause.Components;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
/// The wiring the order terminal suites share: a world holding the money and ordering services, one terminal with
/// its UI over a three-item catalog (one cheap, one dear, one locked), and a cursor owner, all registered in the
/// test service container. The caller adds <see cref="World" /> to the tree.
/// </summary>
internal sealed class OrderTerminalFixture
{
    public const int StartingBalance = 150;
    public const int CheapPrice = 100;
    private const int DearPrice = 400;
    private const int LockedPrice = 10;
    private const string LockedId = "locked";

    public OrderTerminalFixture()
    {
        World = new Node3D();
        var marker = new DeliveryMarker();
        World.AddChild(marker);
        Money = new MoneyService { StartingBalance = StartingBalance };
        Ordering = new OrderingService
            { DeliveryPoint = marker, SpawnRoot = World, Money = Money, Progression = new OpenProgression(LockedId) };
        Ui = new OrderTerminalUi();
        Terminal = new OrderTerminal
        {
            Catalog = MakeCatalog(("cheap", CheapPrice), ("dear", DearPrice), (LockedId, LockedPrice)), Ui = Ui
        };
        Cursor = new CursorOwner();
        World.AddChild(Money);
        World.AddChild(Ordering);
        World.AddChild(Ui);
        World.AddChild(Cursor);

        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IMoneyService>(Money);
        TestServices.Container.RegisterSingleton<IOrderingService>(Ordering);
        TestServices.Container.RegisterSingleton<ICursorOwner>(Cursor);

        World.AddChild(Terminal);
    }

    public Node3D World { get; }
    public MoneyService Money { get; }
    public OrderingService Ordering { get; }
    public OrderTerminalUi Ui { get; }
    public OrderTerminal Terminal { get; }
    public CursorOwner Cursor { get; }

    public static OrderCatalog MakeCatalog(params (string Id, int Price)[] entries)
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
