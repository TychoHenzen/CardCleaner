using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Puts a <see cref="ScreenTerminal" /> on the backoffice PC screen.
///     Only the shop PC has this node, as a child of its terminal.
///     While the terminal is open the screen camera looks at
///     <see cref="Desktop" />, the pointer forwards the mouse to the screen,
///     and the desktop's Close button closes the terminal.
/// </summary>
public partial class PcDesktopLink : Node
{
    private ScreenTerminal? _terminal;
    private PcDesktop? _desktop;
    private Camera3D? _previousCamera;

    /// <summary>
    ///     The desktop on the PC screen. Shows the ordering app, and is
    ///     hidden while the PC is closed.
    /// </summary>
    [Export]
    public PcDesktop? Desktop { get; set; }

    /// <summary>
    ///     Camera that looks at the PC screen while the desktop is open.
    /// </summary>
    [Export]
    public Camera3D? ScreenCamera { get; set; }

    /// <summary>
    ///     Forwards the mouse to the PC screen while the desktop is open.
    /// </summary>
    [Export]
    public PcScreenPointer? ScreenPointer { get; set; }

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
        if (ScreenCamera != null)
            ScreenCamera.Current = true;
        if (ScreenPointer != null)
            ScreenPointer.Active = true;
        _desktop.ResetToDesktop();
        _desktop.Visible = true;
    }

    private void OnTerminalClosed()
    {
        if (_desktop == null)
            return;

        if (ScreenPointer != null)
            ScreenPointer.Active = false;
        if (ScreenCamera != null)
            ScreenCamera.Current = false;
        if (_previousCamera != null
            && GodotObject.IsInstanceValid(_previousCamera))
            _previousCamera.Current = true;
        _previousCamera = null;
        _desktop.ResetToDesktop();
        _desktop.Visible = false;
    }

    private void RequestTerminalClose() => _terminal?.Close();
}
