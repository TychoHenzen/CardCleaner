using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Pause.Components;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Shared harness for the checkout scene suite: loads the shop scene with its services, lays the test window
/// out at its real size, and exposes the counter, its payment screen and the scene's money and cursor, so the
/// suite only states its scenario.
/// </summary>
public sealed class ShopCheckoutRig
{
    private static readonly Vector2I WindowSize = new(1280, 720);

    private ShopCheckoutRig(Node3D shop)
    {
        Shop = shop;
        Counter = shop.GetNode<CheckoutCounter>("World/Storefront/CheckoutCounter");
        Money = shop.GetNode<MoneyService>("Services/MoneyService");
        Cursor = shop.GetNode<CursorOwner>("Services/CursorOwner");
        Ui = shop.GetNode<CheckoutPaymentUi>("CheckoutUi");
        Progression = shop.GetNode<ShopProgression>("Services/ShopProgression");
        Hud = shop.GetNode<ShopHud>("DayUi/Hud");
    }

    public Node3D Shop { get; }
    public ShopProgression Progression { get; }
    public ShopHud Hud { get; }
    public CheckoutCounter Counter { get; }
    public MoneyService Money { get; }
    public CursorOwner Cursor { get; }
    public CheckoutPaymentUi Ui { get; }

    public static async Task<ShopCheckoutRig> Create()
    {
        // A headless window is 64 x 64 and would collapse the payment screen, so size it before the scene enters.
        ((SceneTree)Engine.GetMainLoop()).Root.Size = WindowSize;
        var shop = ShopOrderingRig.LoadShopWithServices();
        var rig = new ShopCheckoutRig(shop);

        AddNode(shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        return rig;
    }

    /// <summary>The item props the counter has laid out for the current basket.</summary>
    public List<CheckoutItemProp> Props() => Counter.GetChildren().OfType<CheckoutItemProp>().ToList();

    /// <summary>Scans every item of the current basket, as the player would by interacting with each prop.</summary>
    public void ScanAll()
    {
        foreach (var prop in Props())
        {
            prop.Interact();
        }
    }

    /// <summary>
    /// Clicks a control with the left button: a press, a frame, then the release. A press without its release
    /// keeps Godot routing every later click to that control.
    /// </summary>
    public static async Task Click(Control control)
    {
        // The layout is only settled after a frame, so the rect is read after one.
        await ISceneRunner.SyncProcessFrame;
        var position = control.GetGlobalRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left,
            Position = position, GlobalPosition = position
        });
        Input.FlushBufferedEvents();
        await ISceneRunner.SyncProcessFrame;
        Input.ParseInputEvent(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false, ButtonMask = 0,
            Position = position, GlobalPosition = position
        });
        Input.FlushBufferedEvents();
        await ISceneRunner.SyncProcessFrame;
    }
}
