// ASSUMPTION: the forwarded click that pressed an item row (ForwardedClickOnStockIconThenOnAnItemOrders) was removed
// because the Stock app holding those rows is built later; its forwarded click to checkout is covered end to end by
// Tests/Scenes/ShopStockAppClickPathSceneTest.cs, the equivalent of that removed test.
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class PcDesktopTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";
    private static readonly string[] DisabledIcons = { "FurnitureIcon", "ExpansionsIcon", "StaffIcon" };
    private static readonly Vector2I ScreenSize = new(1280, 720);

    [TestCase]
    public static void StockIconShowsTheOrderingList()
    {
        var (desktop, stockApp, _) = BuildDesktop();
        AssertThat(stockApp.Visible).IsFalse();

        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsTrue();
    }

    [TestCase]
    public static void DisabledIconsDoNothing()
    {
        var (desktop, stockApp, billsApp) = BuildDesktop();

        foreach (var name in DisabledIcons)
        {
            var icon = Icon(desktop, name);
            AssertThat(icon.Disabled).IsTrue();
            AssertThat(icon.TooltipText).IsEqual("Not installed");
            icon.EmitSignal(BaseButton.SignalName.Pressed);
        }

        AssertThat(stockApp.Visible).IsFalse();
        AssertThat(billsApp.Visible).IsFalse();
    }

    [TestCase]
    public static void CloseButtonRaisesCloseRequested()
    {
        var (desktop, _, _) = BuildDesktop();
        int raised = 0;
        desktop.CloseRequested += () => raised++;

        desktop.GetNode<Button>("Taskbar/CloseButton").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(raised).IsEqual(1);
    }

    [TestCase]
    public static void ResetToDesktopHidesTheOrderingList()
    {
        var (desktop, stockApp, _) = BuildDesktop();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    public static void BillsIconShowsBillsAppAndHidesStock()
    {
        var (desktop, stockApp, billsApp) = BuildDesktop();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        Icon(desktop, "BillsIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(billsApp.Visible).IsTrue();
        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    public static void StockIconHidesBillsApp()
    {
        var (desktop, stockApp, billsApp) = BuildDesktop();
        Icon(desktop, "BillsIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(billsApp.Visible).IsTrue();

        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsTrue();
        AssertThat(billsApp.Visible).IsFalse();
    }

    [TestCase]
    public static void HomeButtonReturnsToDesktop()
    {
        var (desktop, stockApp, billsApp) = BuildDesktop();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.GetNode<Button>("Taskbar/HomeButton").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsFalse();
        AssertThat(billsApp.Visible).IsFalse();
    }

    [TestCase]
    public static void UnassignedBillsAppKeepsIconDisabled()
    {
        var (desktop, _, billsApp) = BuildDesktop(assignBills: false);
        var icon = Icon(desktop, "BillsIcon");
        AssertThat(icon.Disabled).IsTrue();
        AssertThat(icon.TooltipText).IsEqual("Not installed");

        icon.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(billsApp.Visible).IsFalse();
    }

    [TestCase]
    public static void FurnitureIconShowsFurnitureAppAndHidesOthers()
    {
        var (desktop, stockApp, furnitureApp) = BuildDesktopWithFurniture();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        Icon(desktop, "FurnitureIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(furnitureApp.Visible).IsTrue();
        AssertThat(stockApp.Visible).IsFalse();

        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsTrue();
        AssertThat(furnitureApp.Visible).IsFalse();
    }

    [TestCase]
    public static void ResetToDesktopHidesTheFurnitureApp()
    {
        var (desktop, _, furnitureApp) = BuildDesktopWithFurniture();
        Icon(desktop, "FurnitureIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(furnitureApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(furnitureApp.Visible).IsFalse();
    }

    [TestCase]
    public static void AssignedFurnitureIconIsEnabled()
    {
        var (desktop, _, _) = BuildDesktopWithFurniture();

        AssertThat(Icon(desktop, "FurnitureIcon").Disabled).IsFalse();
    }

    [TestCase]
    public static async Task ForwardedClickOnDisabledIconRaisesNothing()
    {
        var (desktop, stockApp, overlay) = await BuildForwardedDesktop();
        int closes = 0;
        desktop.CloseRequested += () => closes++;

        Click(overlay, Centre(desktop.GetNode<Button>("Icons/FurnitureIcon")));
        await WaitForFrames(1);

        AssertThat(stockApp.Visible).IsFalse();
        AssertThat(closes).IsEqual(0);
    }

    /// <summary>
    ///     Builds the desktop inside a 1280 x 720 SubViewport, with its apps as plain Controls, and an active
    ///     <see cref="PcScreenOverlay" /> that shows it in a 1280 x 720 host viewport, so a window position is the
    ///     same SubViewport pixel. Waits until the containers have laid the controls out.
    /// </summary>
    private static async Task<(PcDesktop Desktop, Control StockApp, PcScreenOverlay Overlay)> BuildForwardedDesktop()
    {
        Node world = AddNode(new Node());

        var screen = new SubViewport { Size = ScreenSize };
        var (desktop, stockApp, _) = InstantiateDesktop();
        screen.AddChild(desktop);
        world.AddChild(screen);

        var host = new SubViewport { Size = ScreenSize };
        var overlay = new PcScreenOverlay { Viewport = screen };
        world.AddChild(host);
        host.AddChild(overlay);
        overlay.Activate(0f);

        await WaitForFrames(2);
        return (desktop, stockApp, overlay);
    }

    /// <summary>Sends a left press and release at a window position, as a player's click would arrive.</summary>
    private static void Click(PcScreenOverlay overlay, Vector2 position)
    {
        foreach (var pressed in new[] { true, false })
        {
            overlay._Input(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = pressed,
                Position = position
            });
        }
    }

    /// <summary>The SubViewport pixel at the centre of a control.</summary>
    private static Vector2 Centre(Control control) => control.GlobalPosition + control.Size / 2f;

    private static async Task WaitForFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ISceneRunner.SyncProcessFrame;
    }

    /// <summary>
    ///     Instantiates the desktop scene and gives it two hidden plain Control apps, named StockApp and BillsApp.
    ///     Each app is a child of the desktop. Without <paramref name="assignBills" /> the Bills slot stays empty.
    /// </summary>
    private static DesktopParts InstantiateDesktop(bool assignBills = true)
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        var stockApp = NewApp("StockApp", desktop);
        var billsApp = NewApp("BillsApp", desktop);
        desktop.StockApp = stockApp;
        if (assignBills)
            desktop.BillsApp = billsApp;
        return new DesktopParts(desktop, stockApp, billsApp);
    }

    private static Control NewApp(string name, Node parent)
    {
        var app = new Control { Name = name, Visible = false };
        parent.AddChild(app);
        return app;
    }

    /// <summary>Adds a desktop built from its scene to the test root, with its two apps assigned.</summary>
    private static DesktopParts BuildDesktop(bool assignBills = true)
    {
        var parts = InstantiateDesktop(assignBills);
        AddNode(parts.Desktop);
        return parts;
    }

    /// <summary>Adds a desktop with its Stock app and a hidden Furniture app; the Bills slot stays empty.</summary>
    private static FurnitureDesktopParts BuildDesktopWithFurniture()
    {
        var (desktop, stockApp, _) = InstantiateDesktop(assignBills: false);
        var furnitureApp = NewApp("FurnitureApp", desktop);
        desktop.FurnitureApp = furnitureApp;
        AddNode(desktop);
        return new FurnitureDesktopParts(desktop, stockApp, furnitureApp);
    }

    private readonly record struct FurnitureDesktopParts(PcDesktop Desktop, Control StockApp, Control FurnitureApp);

    private readonly record struct DesktopParts(PcDesktop Desktop, Control StockApp, Control BillsApp);

    private static Button Icon(PcDesktop desktop, string name) => desktop.GetNode<Button>($"Icons/{name}");
}
