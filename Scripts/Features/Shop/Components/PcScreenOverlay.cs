using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Shows the PC desktop fullscreen while the PC is in use and forwards the player's mouse and keys to it.
///     The desktop lives in a SubViewport that the 3D monitor also shows; while active, this layer draws that
///     SubViewport over the whole window (scaled to fit, centred, black bars where the aspect differs) and pushes
///     every mouse event into it at the matching pixel, so the desktop is used like a real screen.
///     Escape is left alone so the terminal can close the PC.
/// </summary>
public partial class PcScreenOverlay : CanvasLayer
{
    // Above the HUD and below the pause menu (layer 10).
    private const int OverlayLayer = 8;
    private const string CloseAction = "ui_cancel";

    private ColorRect? _backdrop;
    private TextureRect? _screen;
    private Tween? _fade;

    /// <summary>The SubViewport that holds the desktop.</summary>
    [Export]
    public SubViewport? Viewport { get; set; }

    /// <summary>True while the desktop is shown fullscreen and receives the player's input.</summary>
    public bool Active { get; private set; }

    /// <summary>The SubViewport pixel of the last forwarded mouse event, for tests.</summary>
    internal Vector2? LastForwardedPixel { get; private set; }

    /// <summary>
    ///     Maps a window position to the SubViewport pixel drawn there when the SubViewport is scaled to fit the
    ///     window and centred. Returns false for a position on the bars beside or above the drawn screen.
    /// </summary>
    public static bool TryMapToPixel(Vector2 position, Vector2 windowSize, Vector2 viewportSize, out Vector2 pixel)
    {
        pixel = Vector2.Zero;
        if (viewportSize.X <= 0f || viewportSize.Y <= 0f || windowSize.X <= 0f || windowSize.Y <= 0f)
            return false;

        float scale = FitScale(windowSize, viewportSize);
        Vector2 offset = (windowSize - viewportSize * scale) / 2f;
        pixel = (position - offset) / scale;
        return pixel.X >= 0f && pixel.Y >= 0f && pixel.X <= viewportSize.X && pixel.Y <= viewportSize.Y;
    }

    private static float FitScale(Vector2 windowSize, Vector2 viewportSize) =>
        Mathf.Min(windowSize.X / viewportSize.X, windowSize.Y / viewportSize.Y);

    public override void _Ready()
    {
        Layer = OverlayLayer;
        _backdrop = new ColorRect
        {
            Name = "Backdrop", Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _screen = new TextureRect
        {
            Name = "Screen",
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            // The project default is nearest, which would make a scaled desktop blocky.
            TextureFilter = CanvasItem.TextureFilterEnum.Linear,
        };
        _screen.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _backdrop.AddChild(_screen);
        AddChild(_backdrop);
        if (Viewport != null)
            _screen.Texture = Viewport.GetTexture();
        SetShown(false);
    }

    /// <summary>Shows the desktop fullscreen, fading it in over <paramref name="fadeSeconds" />, and takes input.</summary>
    public void Activate(float fadeSeconds)
    {
        Active = true;
        SetShown(true);
        _fade?.Kill();
        if (_backdrop == null || fadeSeconds <= 0f || !IsInsideTree())
            return;

        _backdrop.Modulate = new Color(1f, 1f, 1f, 0f);
        _fade = CreateTween();
        _fade.TweenProperty(_backdrop, "modulate:a", 1f, fadeSeconds)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    /// <summary>Hides the desktop at once and stops taking input.</summary>
    public void Deactivate()
    {
        Active = false;
        _fade?.Kill();
        _fade = null;
        SetShown(false);
    }

    public override void _Input(InputEvent @event)
    {
        if (!Active || Viewport == null)
            return;

        switch (@event)
        {
            case InputEventMouse mouse:
                ForwardMouse(mouse);
                GetViewport().SetInputAsHandled();
                break;
            case InputEventKey key when !key.IsAction(CloseAction):
                Viewport.PushInput(key.Duplicate() as InputEvent, true);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    // The window keeps every mouse event while the desktop is up, so a click on the bars never reaches the game.
    private void ForwardMouse(InputEventMouse mouse)
    {
        Vector2 windowSize = GetViewport().GetVisibleRect().Size;
        Vector2 viewportSize = new(Viewport!.Size.X, Viewport.Size.Y);
        if (!TryMapToPixel(mouse.Position, windowSize, viewportSize, out Vector2 pixel))
            return;

        var forwarded = (InputEventMouse)mouse.Duplicate();
        forwarded.Position = pixel;
        forwarded.GlobalPosition = pixel;
        if (forwarded is InputEventMouseMotion motion)
        {
            float toPixels = 1f / FitScale(windowSize, viewportSize);
            motion.Relative *= toPixels;
            motion.ScreenRelative *= toPixels;
        }

        Viewport.PushInput(forwarded, true);
        LastForwardedPixel = pixel;
    }

    private void SetShown(bool shown)
    {
        Visible = shown;
        if (_backdrop != null)
        {
            _backdrop.Visible = shown;
            _backdrop.Modulate = Colors.White;
        }
    }
}
