using System;
using Godot;
using CardCleaner.Scripts.Features.Shop.Screens;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class PcScreenOverlayTest
{
    private static readonly Vector2 Viewport = new(1280f, 720f);
    private static readonly Vector2 CentreOfScreen = new(640f, 360f);
    private const float Tolerance = 0.01f;

    [TestCase]
    public static void SameSizeWindowMapsOneToOne()
    {
        bool hit = PcScreenOverlay.TryMapToPixel(new Vector2(100f, 200f), Viewport, Viewport, out Vector2 pixel);

        AssertThat(hit).IsTrue();
        AssertThat(IsNear(pixel, new Vector2(100f, 200f))).IsTrue();
    }

    [TestCase]
    public static void LargerWindowScalesDown()
    {
        bool hit = PcScreenOverlay.TryMapToPixel(
            new Vector2(1920f, 1080f), new Vector2(1920f, 1080f), Viewport, out Vector2 pixel);

        AssertThat(hit).IsTrue();
        AssertThat(IsNear(pixel, Viewport)).IsTrue();
    }

    [TestCase]
    public static void TallerWindowLetterboxesAboveAndBelow()
    {
        // A 1280 x 1024 window draws the 1280 x 720 screen between 152 px bars.
        var window = new Vector2(1280f, 1024f);

        bool top = PcScreenOverlay.TryMapToPixel(new Vector2(640f, 152f), window, Viewport, out Vector2 topPixel);
        bool bar = PcScreenOverlay.TryMapToPixel(new Vector2(640f, 100f), window, Viewport, out _);

        AssertThat(top).IsTrue();
        AssertThat(IsNear(topPixel, new Vector2(640f, 0f))).IsTrue();
        AssertThat(bar).IsFalse();
    }

    [TestCase]
    public static void WiderWindowPillarboxesLeftAndRight()
    {
        // A 1920 x 720 window draws the screen between 320 px bars.
        var window = new Vector2(1920f, 720f);

        bool left = PcScreenOverlay.TryMapToPixel(new Vector2(320f, 360f), window, Viewport, out Vector2 leftPixel);
        bool bar = PcScreenOverlay.TryMapToPixel(new Vector2(1700f, 360f), window, Viewport, out _);

        AssertThat(left).IsTrue();
        AssertThat(IsNear(leftPixel, new Vector2(0f, 360f))).IsTrue();
        AssertThat(bar).IsFalse();
    }

    [TestCase]
    public static void EmptyViewportMapsToNone()
    {
        bool hit = PcScreenOverlay.TryMapToPixel(CentreOfScreen, Viewport, Vector2.Zero, out _);

        AssertThat(hit).IsFalse();
    }

    [TestCase]
    public static void ActivateShowsTheScreenAndDeactivateHidesIt()
    {
        PcScreenOverlay overlay = BuildScreen(out _);

        overlay.Activate(0f);
        AssertThat(overlay.Active).IsTrue();
        AssertThat(overlay.Visible).IsTrue();

        overlay.Deactivate();
        AssertThat(overlay.Active).IsFalse();
        AssertThat(overlay.Visible).IsFalse();
    }

    [TestCase]
    public static void MotionIsForwardedAndHandled()
    {
        PcScreenOverlay overlay = BuildScreen(out _);
        overlay.Activate(0f);

        overlay._Input(new InputEventMouseMotion { Position = CentreOfScreen });

        AssertThat(overlay.LastForwardedPixel.HasValue).IsTrue();
        AssertThat(IsNear(overlay.LastForwardedPixel!.Value, CentreOfScreen)).IsTrue();
        AssertThat(overlay.GetViewport().IsInputHandled()).IsTrue();
    }

    [TestCase]
    public static void InactiveOverlayLeavesInputAlone()
    {
        PcScreenOverlay overlay = BuildScreen(out _);

        overlay._Input(new InputEventMouseMotion { Position = CentreOfScreen });

        AssertThat(overlay.LastForwardedPixel.HasValue).IsFalse();
        AssertThat(overlay.GetViewport().IsInputHandled()).IsFalse();
    }

    [TestCase]
    public static void PressAndReleaseOnAButtonInsideTheSubViewportFiresPressed()
    {
        PcScreenOverlay overlay = BuildScreen(out Button button);
        overlay.Activate(0f);
        int pressed = 0;
        button.Pressed += () => pressed++;

        overlay._Input(Click(MouseButton.Left, true, CentreOfScreen));
        overlay._Input(Click(MouseButton.Left, false, CentreOfScreen));

        AssertThat(pressed).IsEqual(1);
    }

    [TestCase]
    public static void WheelPressAndReleaseAreForwarded()
    {
        PcScreenOverlay overlay = BuildScreen(out _);
        overlay.Activate(0f);

        overlay._Input(Click(MouseButton.WheelUp, true, CentreOfScreen));
        AssertThat(IsNear(overlay.LastForwardedPixel!.Value, CentreOfScreen)).IsTrue();

        var releasePosition = new Vector2(650f, 360f);
        overlay._Input(Click(MouseButton.WheelUp, false, releasePosition));
        AssertThat(IsNear(overlay.LastForwardedPixel!.Value, releasePosition)).IsTrue();
    }

    [TestCase]
    public static void EscapeIsLeftForTheTerminal()
    {
        PcScreenOverlay overlay = BuildScreen(out _);
        overlay.Activate(0f);

        overlay._Input(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });

        AssertThat(overlay.GetViewport().IsInputHandled()).IsFalse();
    }

    [TestCase]
    public static void OtherKeysAreKeptFromTheGame()
    {
        PcScreenOverlay overlay = BuildScreen(out _);
        overlay.Activate(0f);

        overlay._Input(new InputEventKey { Keycode = Key.W, PhysicalKeycode = Key.W, Pressed = true });

        AssertThat(overlay.GetViewport().IsInputHandled()).IsTrue();
    }

    /// <summary>
    ///     Builds a 1280 x 720 SubViewport holding a full-size Button and an overlay that shows it. The overlay sits
    ///     in its own 1280 x 720 host viewport, so the "input handled" flag it sets is private to this test and a
    ///     window position equals the SubViewport pixel. Returns the overlay; the Button is the one inside.
    /// </summary>
    private static PcScreenOverlay BuildScreen(out Button button)
    {
        Node world = AddNode(new Node());

        var subViewport = new SubViewport { Size = new Vector2I(1280, 720) };
        button = new Button { Size = Viewport };
        subViewport.AddChild(button);
        world.AddChild(subViewport);

        var host = new SubViewport { Size = new Vector2I(1280, 720) };
        var overlay = new PcScreenOverlay { Viewport = subViewport };
        world.AddChild(host);
        host.AddChild(overlay);
        return overlay;
    }

    private static InputEventMouseButton Click(MouseButton button, bool pressed, Vector2 position) => new()
    {
        ButtonIndex = button,
        Pressed = pressed,
        Position = position,
    };

    private static bool IsNear(Vector2 actual, Vector2 expected) =>
        Math.Abs(actual.X - expected.X) < Tolerance && Math.Abs(actual.Y - expected.Y) < Tolerance;
}
