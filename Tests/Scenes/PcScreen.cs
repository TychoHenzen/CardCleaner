using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The backoffice PC screen as a test sees it: the desktop viewport, drawn fullscreen by the screen overlay while
///     the PC is open, and the camera that looks at the monitor. Turns a control on the desktop into the window pixel
///     where a mouse click hits it, and sends real input to the nodes.
/// </summary>
internal sealed class PcScreen(SubViewport viewport, Camera3D camera)
{
    private const string ViewportPath = "PcDesktopViewport";
    private const string TerminalPath = "World/Markers/PcLocation/PcTerminal";

    public Camera3D Camera => camera;

    public static PcScreen From(Node shop) => new(
        shop.GetNode<SubViewport>(ViewportPath),
        shop.GetNode<Camera3D>($"{TerminalPath}/ScreenCamera"));

    // Maps a control on the desktop viewport to the window pixel where the mouse must click to hit it. The overlay
    // scales the desktop to fit the window and centres it, so the control's pixel is scaled and offset the same way.
    public Vector2 PositionOf(Control control)
    {
        Vector2 pixel = control.GetGlobalRect().GetCenter();
        Vector2 window = camera.GetTree().Root.GetVisibleRect().Size;
        Vector2 size = new(viewport.Size.X, viewport.Size.Y);
        float scale = Mathf.Min(window.X / size.X, window.Y / size.Y);
        return (window - size * scale) / 2f + pixel * scale;
    }

    public Task Click(Control control) => ClickAt(PositionOf(control));

    // Each injected event is flushed to the nodes as it is sent: ParseInputEvent only queues it, and without the
    // flush the event would not reach _Input or _UnhandledInput before the test reads the result. Each click waits
    // two frames so queued frees (rebuilt rows) run before the test reads the result.
    public static async Task ClickAt(Vector2 position)
    {
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = true, Position = position, GlobalPosition = position
        });
        await ISceneRunner.SyncPhysicsFrame;
        Send(new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left, Pressed = false, Position = position, GlobalPosition = position
        });
        await ISceneRunner.SyncPhysicsFrame;
    }

    // Sent as the ui_cancel action, the same way ShopCheckoutSceneTest closes its screens.
    public static async Task PressEscape()
    {
        Send(new InputEventAction { Action = "ui_cancel", Pressed = true });
        await ISceneRunner.SyncPhysicsFrame;
        Send(new InputEventAction { Action = "ui_cancel", Pressed = false });
        await ISceneRunner.SyncPhysicsFrame;
    }

    private static void Send(InputEvent input)
    {
        Input.ParseInputEvent(input);
        Input.FlushBufferedEvents();
    }
}
