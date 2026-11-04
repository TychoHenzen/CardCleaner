// ASSUMPTION: the forwarded click that pressed an item row (ForwardedClickOnStockIconThenOnAnItemOrders) was removed
// because the Stock app holding those rows is built later; its forwarded click to checkout is covered end to end by
// Tests/Scenes/ShopStockAppClickPathSceneTest.cs, the equivalent of that removed test.
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Ui;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class PcDesktopTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";
    private static readonly string[] DisabledIcons = { "FurnitureIcon", "ExpansionsIcon", "StaffIcon" };
    private static readonly Vector2I ScreenSize = new(1280, 720);
    private static readonly Vector2 QuadSize = new(1f, 1f);

    [TestCase]
    [TestCategory("Unit")]
    public static void StockIconShowsTheOrderingList()
    {
        var (desktop, stockApp, _) = BuildDesktop();
        AssertThat(stockApp.Visible).IsFalse();

        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public static void CloseButtonRaisesCloseRequested()
    {
        var (desktop, _, _) = BuildDesktop();
        int raised = 0;
        desktop.CloseRequested += () => raised++;

        desktop.GetNode<Button>("Taskbar/CloseButton").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(raised).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResetToDesktopHidesTheOrderingList()
    {
        var (desktop, stockApp, _) = BuildDesktop();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BillsIconShowsBillsAppAndHidesStock()
    {
        var (desktop, stockApp, billsApp) = BuildDesktop();
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        Icon(desktop, "BillsIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(billsApp.Visible).IsTrue();
        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public static async Task ForwardedClickOnDisabledIconRaisesNothing()
    {
        var (desktop, stockApp, pointer, camera) = await BuildForwardedDesktop();
        int closes = 0;
        desktop.CloseRequested += () => closes++;

        Click(pointer, camera, Centre(desktop.GetNode<Button>("Icons/FurnitureIcon")));
        await WaitForFrames(1);

        AssertThat(stockApp.Visible).IsFalse();
        AssertThat(closes).IsEqual(0);
    }

    /// <summary>
    ///     Builds the desktop inside a 1280 x 720 SubViewport, with its apps as plain Controls, and a camera 2 m
    ///     in front of a 1 x 1 m quad that shows that SubViewport, wired to a <see cref="PcScreenPointer" />.
    ///     Waits until the containers have laid the controls out.
    /// </summary>
    private static async Task<(PcDesktop Desktop, Control StockApp, PcScreenPointer Pointer, Camera3D Camera)>
        BuildForwardedDesktop()
    {
        Node3D world = AddNode(new Node3D());
        world.GetTree().Root.Size = ScreenSize;

        var screen = new SubViewport { Size = ScreenSize };
        var desktop = InstantiateDesktop(out var stockApp, out _);
        screen.AddChild(desktop);
        world.AddChild(screen);

        var camera = new Camera3D { Position = new Vector3(0f, 0f, 2f) };
        var quad = new MeshInstance3D { Mesh = new QuadMesh { Size = QuadSize } };
        var host = new SubViewport();
        var pointer = new PcScreenPointer
        {
            ScreenCamera = camera,
            ScreenQuad = quad,
            Viewport = screen,
            Active = true
        };
        world.AddChild(camera);
        world.AddChild(quad);
        world.AddChild(host);
        host.AddChild(pointer);

        await WaitForFrames(2);
        return (desktop, stockApp, pointer, camera);
    }

    /// <summary>Sends a left press and release at a screen position, as a player's click would arrive.</summary>
    private static void Click(PcScreenPointer pointer, Camera3D camera, Vector2 pixel)
    {
        Vector2 screenPosition = ScreenPositionOf(camera, pixel);
        foreach (var pressed in new[] { true, false })
        {
            pointer._Input(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = pressed,
                Position = screenPosition
            });
        }
    }

    /// <summary>Maps a SubViewport pixel to the quad point showing it, then projects it to the screen.</summary>
    private static Vector2 ScreenPositionOf(Camera3D camera, Vector2 pixel)
    {
        var quadPoint = new Vector3(pixel.X / ScreenSize.X - 0.5f, 0.5f - pixel.Y / ScreenSize.Y, 0f);
        return camera.UnprojectPosition(quadPoint);
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
    private static PcDesktop InstantiateDesktop(out Control stockApp, out Control billsApp, bool assignBills = true)
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        stockApp = NewApp("StockApp", desktop);
        billsApp = NewApp("BillsApp", desktop);
        desktop.StockApp = stockApp;
        if (assignBills)
            desktop.BillsApp = billsApp;
        return desktop;
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
        var desktop = InstantiateDesktop(out var stockApp, out var billsApp, assignBills);
        AddNode(desktop);
        return new DesktopParts(desktop, stockApp, billsApp);
    }

    private readonly record struct DesktopParts(PcDesktop Desktop, Control StockApp, Control BillsApp);

    private static Button Icon(PcDesktop desktop, string name) => desktop.GetNode<Button>($"Icons/{name}");
}
