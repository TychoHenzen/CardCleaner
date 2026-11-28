using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Screens;

/// <summary>
///     Puts a <see cref="ScreenTerminal" /> on the backoffice PC screen.
///     Only the shop PC has this node, as a child of its terminal.
///     Opening the terminal moves the view from the player's eyes to the screen camera in front of the monitor while
///     the screen overlay fades the desktop in fullscreen; from then on the overlay forwards the mouse and keys to the
///     desktop. Closing hides the overlay and gives the player's camera back at once. The desktop's Close button
///     closes the terminal.
/// </summary>
public partial class PcDesktopLink : Node
{
    /// <summary>Seconds the view takes to move to the screen when the PC opens.</summary>
    public const float FocusSeconds = 0.35f;

    private ScreenTerminal? _terminal;
    private PcDesktop? _desktop;
    private Camera3D? _previousCamera;
    private Transform3D _screenCameraHome;
    private Tween? _focus;

    /// <summary>
    ///     The desktop on the PC screen. It is shown, reset to its icons,
    ///     while the PC is open, and hidden while the PC is closed.
    /// </summary>
    [Export]
    public PcDesktop? Desktop { get; set; }

    /// <summary>
    ///     Camera that looks at the PC screen while the desktop is open. Its authored pose is where the view ends up.
    /// </summary>
    [Export]
    public Camera3D? ScreenCamera { get; set; }

    /// <summary>Draws the desktop fullscreen and forwards input to it while the PC is open.</summary>
    [Export]
    public PcScreenOverlay? ScreenOverlay { get; set; }

    public override void _Ready()
    {
        _terminal = GetParent() as ScreenTerminal;
        _desktop = Desktop;
        if (_terminal == null || _desktop == null)
        {
            ILog.Error(
                $"{Name} must be a child of a ScreenTerminal "
                + "and needs a Desktop");
            return;
        }

        if (ScreenCamera != null)
            _screenCameraHome = ScreenCamera.Transform;
        _terminal.Opened += OnTerminalOpened;
        _terminal.Closed += OnTerminalClosed;
        _desktop.CloseRequested += RequestTerminalClose;
    }

    // No _ExitTree handler: the terminal's own exit runs Close after this
    // node's exit, and that Close must still restore the player camera.
    // Both nodes leave the tree together, so the subscriptions never
    // outlive the PC.
    private void OnTerminalOpened()
    {
        if (_desktop == null)
            return;

        _previousCamera = GetViewport().GetCamera3D();
        FocusScreenCamera();
        ScreenOverlay?.Activate(FocusSeconds);
        _desktop.ResetToDesktop();
        _desktop.Visible = true;
    }

    private void OnTerminalClosed()
    {
        if (_desktop == null)
            return;

        ScreenOverlay?.Deactivate();
        ReleaseScreenCamera();
        RestorePreviousCamera();
        _desktop.ResetToDesktop();
        _desktop.Visible = false;
    }

    // The screen camera starts where the player's eyes are and moves to its authored pose, so the player sees
    // themselves lean in to the monitor while the desktop fades in over the view.
    private void FocusScreenCamera()
    {
        if (ScreenCamera == null)
            return;

        _focus?.Kill();
        ScreenCamera.Transform = _screenCameraHome;
        Transform3D target = ScreenCamera.GlobalTransform;
        bool canMove = _previousCamera != null && GodotObject.IsInstanceValid(_previousCamera)
                                               && ScreenCamera.IsInsideTree();
        if (canMove)
            ScreenCamera.GlobalTransform = _previousCamera!.GlobalTransform;
        ScreenCamera.Current = true;
        if (!canMove)
            return;

        _focus = ScreenCamera.CreateTween();
        _focus.TweenProperty(ScreenCamera, "global_transform", target, FocusSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.InOut);
    }

    private void ReleaseScreenCamera()
    {
        _focus?.Kill();
        _focus = null;
        if (ScreenCamera == null)
            return;

        ScreenCamera.Current = false;
        ScreenCamera.Transform = _screenCameraHome;
    }

    private void RestorePreviousCamera()
    {
        if (_previousCamera != null
            && GodotObject.IsInstanceValid(_previousCamera))
            _previousCamera.Current = true;
        _previousCamera = null;
    }

    private void RequestTerminalClose() => _terminal?.Close();
}
