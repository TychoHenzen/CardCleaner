using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Ui;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

[TestSuite]
[RequireGodotRuntime]
public class PcDesktopTest
{
    private const string DesktopScene = "res://Scenes/Shop/PcDesktop.tscn";
    private static readonly string[] DisabledIcons = { "FurnitureIcon", "ExpansionsIcon", "StaffIcon", "BillsIcon" };
    private static readonly Vector2I ScreenSize = new(1280, 720);
    private static readonly Vector2 QuadSize = new(1f, 1f);

    [TestCase]
    [TestCategory("Unit")]
    public void StockIconShowsTheOrderingList()
    {
        var desktop = BuildDesktop(out CanvasLayer stockApp);
        AssertThat(stockApp.Visible).IsFalse();

        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(stockApp.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DisabledIconsDoNothing()
    {
        var desktop = BuildDesktop(out CanvasLayer stockApp);

        foreach (var name in DisabledIcons)
        {
            var icon = Icon(desktop, name);
            AssertThat(icon.Disabled).IsTrue();
            AssertThat(icon.TooltipText).IsEqual("Not installed");
            icon.EmitSignal(BaseButton.SignalName.Pressed);
        }

        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CloseButtonRaisesCloseRequested()
    {
        var desktop = BuildDesktop(out _);
        int raised = 0;
        desktop.CloseRequested += () => raised++;

        desktop.GetNode<Button>("Taskbar/CloseButton").EmitSignal(BaseButton.SignalName.Pressed);

        AssertThat(raised).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ResetToDesktopHidesTheOrderingList()
    {
        var desktop = BuildDesktop(out CanvasLayer stockApp);
        Icon(desktop, "StockIcon").EmitSignal(BaseButton.SignalName.Pressed);
        AssertThat(stockApp.Visible).IsTrue();

        desktop.ResetToDesktop();

        AssertThat(stockApp.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ForwardedClickOnStockIconThenOnAnItemOrders()
    {
        var (desktop, terminal, pointer, camera) = await BuildForwardedDesktop();
        int orders = 0;
        terminal.OrderRequested += _ => orders++;

        Click(pointer, camera, Centre(desktop.GetNode<Button>("Icons/StockIcon")));
        await WaitForFrames(2);
        AssertThat(terminal.Visible).IsTrue();
        AssertThat(orders).IsEqual(0);

        var item = FindButton(terminal, "widget");
        AssertThat(item).IsNotNull();
        Click(pointer, camera, Centre(item!));

        AssertThat(orders).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ForwardedClickOnDisabledIconRaisesNothing()
    {
        var (desktop, terminal, pointer, camera) = await BuildForwardedDesktop();
        int orders = 0;
        int closes = 0;
        terminal.OrderRequested += _ => orders++;
        desktop.CloseRequested += () => closes++;

        Click(pointer, camera, Centre(desktop.GetNode<Button>("Icons/FurnitureIcon")));
        await WaitForFrames(1);

        AssertThat(terminal.Visible).IsFalse();
        AssertThat(orders).IsEqual(0);
        AssertThat(closes).IsEqual(0);
    }

    /// <summary>
    ///     Builds the desktop inside a 1280 x 720 SubViewport with a real ordering screen as its stock app
    ///     (one item named "widget"), and a camera 2 m in front of a 1 x 1 m quad that shows that SubViewport,
    ///     wired to a <see cref="PcScreenPointer" />. Waits until the containers have laid the controls out.
    /// </summary>
    private async Task<(PcDesktop Desktop, OrderTerminalUi Terminal, PcScreenPointer Pointer, Camera3D Camera)>
        BuildForwardedDesktop()
    {
        Node3D world = AddNode(new Node3D());
        world.GetTree().Root.Size = ScreenSize;

        var screen = new SubViewport { Size = ScreenSize };
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        var terminal = new OrderTerminalUi();
        terminal.Populate(new OrderCatalog { Items = new[] { MakeItem("widget") } });
        desktop.StockApp = terminal;
        screen.AddChild(desktop);
        screen.AddChild(terminal);
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
        return (desktop, terminal, pointer, camera);
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

    /// <summary>Maps a SubViewport pixel back to the quad point showing it, then projects that point to the screen.</summary>
    private static Vector2 ScreenPositionOf(Camera3D camera, Vector2 pixel)
    {
        var quadPoint = new Vector3(pixel.X / ScreenSize.X - 0.5f, 0.5f - pixel.Y / ScreenSize.Y, 0f);
        return camera.UnprojectPosition(quadPoint);
    }

    /// <summary>The SubViewport pixel at the centre of a control.</summary>
    private static Vector2 Centre(Control control) => control.GlobalPosition + control.Size / 2f;

    private static Button? FindButton(Node root, string textStart)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text.StartsWith(textStart, StringComparison.Ordinal))
                return button;
            if (FindButton(child, textStart) is { } nested)
                return nested;
        }

        return null;
    }

    private static OrderItem MakeItem(string id)
    {
        var root = new Node3D();
        var packed = new PackedScene();
        packed.Pack(root);
        root.Free();
        return new OrderItem { Id = id, DisplayName = id, Price = 5, Scene = packed };
    }

    private static async Task WaitForFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ISceneRunner.SyncProcessFrame;
    }

    /// <summary>Adds a desktop built from its scene, with a hidden stub CanvasLayer as its stock app.</summary>
    private PcDesktop BuildDesktop(out CanvasLayer stockApp)
    {
        stockApp = AddNode(new CanvasLayer { Visible = false });
        var desktop = GD.Load<PackedScene>(DesktopScene).Instantiate<PcDesktop>();
        desktop.StockApp = stockApp;
        AddNode(desktop);
        return desktop;
    }

    private static Button Icon(PcDesktop desktop, string name) => desktop.GetNode<Button>($"Icons/{name}");
}
