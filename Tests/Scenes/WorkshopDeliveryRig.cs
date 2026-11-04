using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene wired for the workshop delivery suites: the backoffice PC with its Stock app, the workshop order
/// terminal and its UI, the delivery markers, the money service, a <see cref="TerminalPresses" /> for the workshop
/// terminal and a <see cref="StockPresses" /> for the PC. The caller adds <see cref="Shop" /> to the tree and awaits
/// <see cref="SettleAsync" />.
/// </summary>
internal sealed class WorkshopDeliveryRig
{
    public const int TopUp = 10000;

    public WorkshopDeliveryRig()
    {
        Shop = ShopOrderingRig.LoadShopWithServices();
        Workshop = Shop.GetNode<Node3D>("World/Workshop");
        Pc = Shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal");
        Terminal = Workshop.GetNode<OrderTerminal>("OrderingRoom/OrderTerminal");
        PcStock = Shop.GetNode<StockApp>("PcDesktopViewport/StockApp");
        Ui = Workshop.GetNode<OrderTerminalUi>("OrderUi");
        Delivery = Workshop.GetNode<DeliveryMarker>("DeliveryPoint");
        ShopDelivery = Shop.GetNode<Marker3D>("World/Markers/DeliveryPoint");
        Money = Shop.GetNode<MoneyService>("Services/MoneyService");
        var ordering = Shop.GetNode<OrderingService>("Services/OrderingService");
        Presses = new TerminalPresses(ordering, Capacity);
        PcPresses = new StockPresses(PcStock, ordering, Shop.GetNode("World"), Capacity);
    }

    public static int Capacity => OrderingService.SlotColumns * OrderingService.SlotRows;

    public Node3D Shop { get; }
    public Node3D Workshop { get; }
    public ScreenTerminal Pc { get; }
    public OrderTerminal Terminal { get; }
    public StockApp PcStock { get; }
    public OrderTerminalUi Ui { get; }
    public MoneyService Money { get; }
    public TerminalPresses Presses { get; }
    public StockPresses PcPresses { get; }
    public DeliveryMarker Delivery { get; }
    public Marker3D ShopDelivery { get; }

    /// <summary>Waits two physics frames so the scene's bodies are registered with the physics space.</summary>
    public static async Task SettleAsync()
    {
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    /// <summary>The workshop catalog index of an item id, or -1 when the catalog has none.</summary>
    public int IndexOf(string id) => Terminal.Catalog!.Items.ToList().FindIndex(i => i.Id == id);
}
