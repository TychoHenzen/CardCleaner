using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>The Staff icon is enabled once an app is assigned; it opens that app and Home closes it.</summary>
[TestSuite]
[RequireGodotRuntime]
public class PcDesktopStaffTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";
    private const string IconPath = "Icons/StaffIcon";
    private static readonly Vector2I ScreenSize = new(1280, 720);

    [TestCase]
    public static void StaffIconShowsStaffAppAndHidesOthers()
    {
        var (desktop, stockApp, staffApp) = BuildDesktop(assignStaff: true);
        desktop.GetNode<Button>("Icons/StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.GetNode<Button>(IconPath).EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(staffApp.Visible).IsTrue();
        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    public static void AssignedStaffIconIsEnabled()
    {
        var (desktop, _, _) = BuildDesktop(assignStaff: true);

        AssertThat(desktop.GetNode<Button>(IconPath).Disabled).IsFalse();
    }

    [TestCase]
    public static void UnassignedStaffIconStaysDisabledWithNotInstalledTooltip()
    {
        var (desktop, _, staffApp) = BuildDesktop(assignStaff: false);
        var icon = desktop.GetNode<Button>(IconPath);
        AssertThat(icon.Disabled).IsTrue();
        AssertThat(icon.TooltipText).IsEqual("Not installed");

        icon.EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(staffApp.Visible).IsFalse();
    }

    [TestCase]
    public static void ResetToDesktopHidesTheStaffApp()
    {
        var (desktop, _, staffApp) = BuildDesktop(assignStaff: true);
        desktop.GetNode<Button>(IconPath).EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(staffApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(staffApp.Visible).IsFalse();
    }

    [TestCase]
    public static async Task ForwardedHome()
    {
        var (desktop, staffApp, overlay) = await BuildForwardedDesktop();

        Click(overlay, Centre(desktop.GetNode<Button>(IconPath)));
        await WaitForFrames(1);
        AssertThat(staffApp.Visible).IsTrue();

        Click(overlay, Centre(desktop.GetNode<Button>("Taskbar/HomeButton")));
        await WaitForFrames(1);

        AssertThat(staffApp.Visible).IsFalse();
    }

    private readonly record struct DesktopParts(PcDesktop Desktop, Control StockApp, Control StaffApp);

    private static DesktopParts InstantiateDesktop(bool assignStaff)
    {
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        var stockApp = NewApp("StockApp", desktop);
        var staffApp = NewApp("StaffApp", desktop);
        desktop.StockApp = stockApp;
        if (assignStaff)
            desktop.StaffApp = staffApp;
        return new DesktopParts(desktop, stockApp, staffApp);
    }

    private static DesktopParts BuildDesktop(bool assignStaff)
    {
        var parts = InstantiateDesktop(assignStaff);
        AddNode(parts.Desktop);
        return parts;
    }

    /// <summary>
    ///     Builds the desktop inside a 1280 x 720 SubViewport with the Staff app assigned, and an active
    ///     <see cref="PcScreenOverlay" /> that shows it in a 1280 x 720 host viewport, so a window position is the
    ///     same SubViewport pixel.
    /// </summary>
    private static async Task<(PcDesktop Desktop, Control StaffApp, PcScreenOverlay Overlay)> BuildForwardedDesktop()
    {
        Node world = AddNode(new Node());

        var screen = new SubViewport { Size = ScreenSize };
        var (desktop, _, staffApp) = InstantiateDesktop(assignStaff: true);
        screen.AddChild(desktop);
        world.AddChild(screen);

        var host = new SubViewport { Size = ScreenSize };
        var overlay = new PcScreenOverlay { Viewport = screen };
        world.AddChild(host);
        host.AddChild(overlay);
        overlay.Activate(0f);

        await WaitForFrames(2);
        return (desktop, staffApp, overlay);
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

    private static Control NewApp(string name, Node parent)
    {
        var app = new Control { Name = name, Visible = false };
        parent.AddChild(app);
        return app;
    }
}
