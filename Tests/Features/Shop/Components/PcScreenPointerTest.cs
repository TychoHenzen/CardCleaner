using System;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class PcScreenPointerTest
{
    private static readonly Vector2 QuadSize = new(1f, 1f);
    private static readonly Vector2I Viewport = new(1280, 720);
    private static readonly Vector2 CentreOfScreen = new(640f, 360f);
    private static readonly Vector2 CentrePixel = new(640f, 360f);
    private const float Tolerance = 0.01f;

    [TestCase]
    [TestCategory("Unit")]
    public void CentreMapsToViewportCentre()
    {
        bool hit = PcScreenPointer.TryMapToPixel(
            new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, -1f),
            Transform3D.Identity, QuadSize, Viewport, out Vector2 pixel);

        AssertThat(hit).IsTrue();
        AssertThat(IsNear(pixel, new Vector2(640f, 360f))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FourCornersMapToViewportCorners()
    {
        var back = new Vector3(0f, 0f, -1f);
        var corners = new[]
        {
            (new Vector3(-0.5f, 0.5f, 2f), new Vector2(0f, 0f)),
            (new Vector3(0.5f, 0.5f, 2f), new Vector2(1280f, 0f)),
            (new Vector3(-0.5f, -0.5f, 2f), new Vector2(0f, 720f)),
            (new Vector3(0.5f, -0.5f, 2f), new Vector2(1280f, 720f)),
        };

        foreach (var (origin, expected) in corners)
        {
            bool hit = PcScreenPointer.TryMapToPixel(
                origin, back, Transform3D.Identity, QuadSize, Viewport, out Vector2 pixel);

            AssertThat(hit).IsTrue();
            AssertThat(IsNear(pixel, expected)).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RayBesideQuadMapsToNone()
    {
        bool hit = PcScreenPointer.TryMapToPixel(
            new Vector3(0.75f, 0f, 2f), new Vector3(0f, 0f, -1f),
            Transform3D.Identity, QuadSize, Viewport, out _);

        AssertThat(hit).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RayFromBehindMapsToNone()
    {
        bool hit = PcScreenPointer.TryMapToPixel(
            new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, -1f),
            Transform3D.Identity, QuadSize, Viewport, out _);

        AssertThat(hit).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ParallelRayMapsToNone()
    {
        bool hit = PcScreenPointer.TryMapToPixel(
            new Vector3(0f, 0f, 2f), new Vector3(1f, 0f, 0f),
            Transform3D.Identity, QuadSize, Viewport, out _);

        AssertThat(hit).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RotatedAndTranslatedQuadStillMapsCentre()
    {
        var basis = Basis.Identity.Rotated(Vector3.Up, Mathf.DegToRad(30f));
        var quad = new Transform3D(basis, new Vector3(1f, 2f, 3f));
        Vector3 normal = quad.Basis.Z;
        Vector3 centre = quad.Origin;

        bool hit = PcScreenPointer.TryMapToPixel(
            centre + normal * 2f, -normal, quad, QuadSize, Viewport, out Vector2 pixel);

        AssertThat(hit).IsTrue();
        AssertThat(IsNear(pixel, new Vector2(640f, 360f))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MotionOverTheQuadIsForwardedAndHandled()
    {
        PcScreenPointer pointer = BuildScreen(out _);
        pointer.Active = true;

        pointer._Input(new InputEventMouseMotion { Position = CentreOfScreen });

        AssertThat(pointer.LastForwardedPixel.HasValue).IsTrue();
        AssertThat(IsNear(pointer.LastForwardedPixel!.Value, CentrePixel)).IsTrue();
        AssertThat(pointer.GetViewport().IsInputHandled()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MotionOutsideTheQuadIsNotForwardedNorHandled()
    {
        PcScreenPointer pointer = BuildScreen(out _);
        pointer.Active = true;

        pointer._Input(new InputEventMouseMotion { Position = new Vector2(10f, 10f) });

        AssertThat(pointer.LastForwardedPixel.HasValue).IsFalse();
        AssertThat(pointer.GetViewport().IsInputHandled()).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PressAndReleaseOnAButtonInsideTheSubViewportFiresPressed()
    {
        PcScreenPointer pointer = BuildScreen(out Button button);
        pointer.Active = true;
        int pressed = 0;
        button.Pressed += () => pressed++;

        pointer._Input(Click(MouseButton.Left, true, CentreOfScreen));
        pointer._Input(Click(MouseButton.Left, false, CentreOfScreen));

        AssertThat(pressed).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WheelPressAndReleaseAreForwarded()
    {
        PcScreenPointer pointer = BuildScreen(out _);
        pointer.Active = true;

        pointer._Input(Click(MouseButton.WheelUp, true, CentreOfScreen));
        AssertThat(pointer.LastForwardedPixel.HasValue).IsTrue();
        AssertThat(IsNear(pointer.LastForwardedPixel!.Value, CentrePixel)).IsTrue();

        // The quad covers only part of the screen, so a screen 10 px right of centre lands right of the pixel centre.
        var releasePosition = new Vector2(650f, 360f);
        pointer._Input(Click(MouseButton.WheelUp, false, releasePosition));
        Vector2 release = pointer.LastForwardedPixel!.Value;
        AssertThat(release.X > CentrePixel.X).IsTrue();
        AssertThat(Math.Abs(release.Y - CentrePixel.Y) < Tolerance).IsTrue();
    }

    /// <summary>
    ///     Builds a SubViewport holding a full-size Button, a camera 2 m in front of a 1 x 1 m quad at the origin
    ///     that shows it, and a pointer wired to both. The pointer sits in its own host viewport so that the
    ///     "input handled" flag it sets is private to this test. Returns the pointer; the Button is the one inside.
    /// </summary>
    private PcScreenPointer BuildScreen(out Button button)
    {
        Node3D world = AddNode(new Node3D());
        world.GetTree().Root.Size = new Vector2I(1280, 720);

        var subViewport = new SubViewport { Size = new Vector2I(1280, 720) };
        button = new Button { Size = new Vector2(1280f, 720f) };
        subViewport.AddChild(button);
        world.AddChild(subViewport);

        var camera = new Camera3D { Position = new Vector3(0f, 0f, 2f) };
        var quad = new MeshInstance3D { Mesh = new QuadMesh { Size = QuadSize } };
        var host = new SubViewport();
        var pointer = new PcScreenPointer { ScreenCamera = camera, ScreenQuad = quad, Viewport = subViewport };
        world.AddChild(camera);
        world.AddChild(quad);
        world.AddChild(host);
        host.AddChild(pointer);
        return pointer;
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
