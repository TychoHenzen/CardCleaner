using System.Linq;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The quad that shows the PC desktop lies on the monitor model's own screen. It used to float 11 cm in front of
///     it, lower and smaller than the screen. The screen is the monitor mesh's surface whose material is named Screen.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PcScreenFitSceneTest
{
    private const string TerminalPath = "World/Markers/PcLocation/PcTerminal";
    private const string ScreenMaterialName = "Screen";
    private const float MaxGap = 0.005f;
    private const float MinFill = 0.95f;
    private const float AspectTolerance = 0.01f;
    private const float FramingTolerance = 0.01f;

    [TestCase]
    [TestCategory("Unit")]
    public static void TheDesktopQuadLiesOnTheMonitorScreen()
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        AddNode(shop);
        try
        {
            var terminal = shop.GetNode<Node3D>(TerminalPath);
            var screen = ScreenBoundsIn(terminal);
            AssertThat(screen.HasValue).IsTrue();
            var bounds = screen!.Value;

            var quad = terminal.GetNode<MeshInstance3D>("ScreenQuad");
            var size = ((QuadMesh)quad.Mesh).Size;
            var centre = quad.Position;

            // The screen faces +Z, toward the chair: the quad sits on it, at most a few millimetres in front.
            var gap = centre.Z - bounds.End.Z;
            AssertThat(gap).IsGreaterEqual(0f);
            AssertThat(gap).IsLessEqual(MaxGap);

            AssertThat(centre.X - size.X / 2).IsGreaterEqual(bounds.Position.X - MaxGap);
            AssertThat(centre.X + size.X / 2).IsLessEqual(bounds.End.X + MaxGap);
            AssertThat(centre.Y - size.Y / 2).IsGreaterEqual(bounds.Position.Y - MaxGap);
            AssertThat(centre.Y + size.Y / 2).IsLessEqual(bounds.End.Y + MaxGap);
            AssertThat(size.X).IsGreaterEqual(bounds.Size.X * MinFill);

            var viewport = shop.GetNode<SubViewport>("PcDesktopViewport");
            var aspect = (float)viewport.Size.X / viewport.Size.Y;
            AssertThat(Mathf.Abs(size.X / size.Y - aspect)).IsLess(AspectTolerance);
        }
        finally
        {
            TestServices.Reset();
        }
    }

    /// <summary>
    ///     Without the monitor art the slot shows its grey placeholder box instead. The quad must lie on that box's
    ///     front face too, inside its outline, or the opaque box hides the desktop in that fallback.
    /// </summary>
    [TestCase]
    [TestCategory("Unit")]
    public static void TheDesktopQuadLiesOnTheFallbackMonitorBox()
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        AddNode(shop);
        try
        {
            var terminal = shop.GetNode<Node3D>(TerminalPath);
            var placeholder = terminal.GetNode<MeshInstance3D>("MonitorArt/Placeholder");
            var box = terminal.GlobalTransform.AffineInverse() * placeholder.GlobalTransform * placeholder.Mesh.GetAabb();

            var quad = terminal.GetNode<MeshInstance3D>("ScreenQuad");
            var size = ((QuadMesh)quad.Mesh).Size;
            var centre = quad.Position;

            var gap = centre.Z - box.End.Z;
            AssertThat(gap).IsGreaterEqual(0f);
            AssertThat(gap).IsLessEqual(MaxGap);
            AssertThat(centre.X - size.X / 2).IsGreaterEqual(box.Position.X);
            AssertThat(centre.X + size.X / 2).IsLessEqual(box.End.X);
            AssertThat(centre.Y - size.Y / 2).IsGreaterEqual(box.Position.Y);
            AssertThat(centre.Y + size.Y / 2).IsLessEqual(box.End.Y);
        }
        finally
        {
            TestServices.Reset();
        }
    }

    /// <summary>
    ///     Opening the PC swings the screen camera to its home pose and then draws the desktop over the whole window.
    ///     The home pose looks straight at the quad from the distance where the quad fills the view height, so the
    ///     swing ends on the same picture the overlay starts with.
    /// </summary>
    [TestCase]
    [TestCategory("Unit")]
    public static void TheScreenCameraEndsFramingTheQuad()
    {
        var shop = ShopOrderingRig.LoadShopWithServices();
        AddNode(shop);
        try
        {
            var terminal = shop.GetNode<Node3D>(TerminalPath);
            var quad = terminal.GetNode<MeshInstance3D>("ScreenQuad");
            var camera = terminal.GetNode<Camera3D>("ScreenCamera");
            var size = ((QuadMesh)quad.Mesh).Size;

            AssertThat(camera.Basis.IsEqualApprox(Basis.Identity)).IsTrue();
            AssertThat(Mathf.Abs(camera.Position.X - quad.Position.X)).IsLess(FramingTolerance);
            AssertThat(Mathf.Abs(camera.Position.Y - quad.Position.Y)).IsLess(FramingTolerance);

            var distance = camera.Position.Z - quad.Position.Z;
            var viewHeight = 2 * distance * Mathf.Tan(Mathf.DegToRad(camera.Fov / 2));
            AssertThat(Mathf.Abs(viewHeight - size.Y)).IsLess(FramingTolerance);
        }
        finally
        {
            TestServices.Reset();
        }
    }

    /// <summary>The bounds of the monitor's Screen surface in the terminal's frame, or null when the art is missing.</summary>
    private static Aabb? ScreenBoundsIn(Node3D terminal)
    {
        var art = terminal.GetNode<Node3D>("MonitorArt");
        var toTerminal = terminal.GlobalTransform.AffineInverse();
        foreach (var instance in art.FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
        {
            var mesh = instance.Mesh;
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetMaterial(surface)?.ResourceName != ScreenMaterialName)
                    continue;
                var vertices = mesh.SurfaceGetArrays(surface)[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                var transform = toTerminal * instance.GlobalTransform;
                var bounds = new Aabb(transform * vertices[0], Vector3.Zero);
                foreach (var vertex in vertices)
                    bounds = bounds.Expand(transform * vertex);
                return bounds;
            }
        }

        return null;
    }
}
