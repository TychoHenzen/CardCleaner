using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop scene wired for the workshop delivery suites: both order terminals, their UIs, the delivery markers,
/// the money service and a <see cref="TerminalPresses" /> that presses items on either terminal. The caller adds
/// <see cref="Shop" /> to the tree and awaits <see cref="SettleAsync" />.
/// </summary>
internal sealed class WorkshopDeliveryRig
{
    public const int TopUp = 10000;

    public WorkshopDeliveryRig()
    {
        Shop = ShopOrderingRig.LoadShopWithServices();
        Workshop = Shop.GetNode<Node3D>("World/Workshop");
        Pc = Shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        Terminal = Workshop.GetNode<OrderTerminal>("OrderingRoom/OrderTerminal");
        PcUi = Shop.GetNode<OrderTerminalUi>("PcDesktopViewport/OrderUi");
        Ui = Workshop.GetNode<OrderTerminalUi>("OrderUi");
        Delivery = Workshop.GetNode<DeliveryMarker>("DeliveryPoint");
        ShopDelivery = Shop.GetNode<Marker3D>("World/Markers/DeliveryPoint");
        Money = Shop.GetNode<MoneyService>("Services/MoneyService");
        Presses = new TerminalPresses(Shop.GetNode<OrderingService>("Services/OrderingService"), Capacity);
    }

    public static int Capacity => OrderingService.SlotColumns * OrderingService.SlotRows;

    public Node3D Shop { get; }
    public Node3D Workshop { get; }
    public OrderTerminal Pc { get; }
    public OrderTerminal Terminal { get; }
    public OrderTerminalUi PcUi { get; }
    public OrderTerminalUi Ui { get; }
    public MoneyService Money { get; }
    public TerminalPresses Presses { get; }
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
