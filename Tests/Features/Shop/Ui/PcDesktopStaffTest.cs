using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Ui;
using GdUnit4;
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
    private static readonly Vector2 QuadSize = new(1f, 1f);

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public static void AssignedStaffIconIsEnabled()
    {
        var (desktop, _, _) = BuildDesktop(assignStaff: true);

        AssertThat(desktop.GetNode<Button>(IconPath).Disabled).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
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
    [TestCategory("Unit")]
    public static void ResetToDesktopHidesTheStaffApp()
    {
        var (desktop, _, staffApp) = BuildDesktop(assignStaff: true);
        desktop.GetNode<Button>(IconPath).EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(staffApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(staffApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ForwardedHome()
    {
        var (desktop, staffApp, pointer, camera) = await BuildForwardedDesktop();

        Click(pointer, camera, Centre(desktop.GetNode<Button>(IconPath)));
        await WaitForFrames(1);
        AssertThat(staffApp.Visible).IsTrue();

        Click(pointer, camera, Centre(desktop.GetNode<Button>("Taskbar/HomeButton")));
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
    ///     Builds the desktop inside a 1280 x 720 SubViewport with the Staff app assigned, and a camera 2 m in front
    ///     of a 1 x 1 m quad that shows that SubViewport, wired to a <see cref="PcScreenPointer" />.
    /// </summary>
    private static async Task<(PcDesktop Desktop, Control StaffApp, PcScreenPointer Pointer, Camera3D Camera)>
        BuildForwardedDesktop()
    {
        Node3D world = AddNode(new Node3D());
        world.GetTree().Root.Size = ScreenSize;

        var screen = new SubViewport { Size = ScreenSize };
        var (desktop, _, staffApp) = InstantiateDesktop(assignStaff: true);
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
        return (desktop, staffApp, pointer, camera);
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

    private static Control NewApp(string name, Node parent)
    {
        var app = new Control { Name = name, Visible = false };
        parent.AddChild(app);
        return app;
    }
}
