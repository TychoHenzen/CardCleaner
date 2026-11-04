using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Forwards mouse input aimed at the backoffice PC screen to the
///     SubViewport that the screen shows.
///     A camera ray is cast through the pointer, mapped onto the screen quad,
///     and the event is pushed into the SubViewport at the matching pixel.
///     Events that miss the screen are left for the rest of the game.
/// </summary>
public partial class PcScreenPointer : Node
{
    private const float ParallelTolerance = 1e-6f;

    /// <summary>Camera that casts the ray through the mouse position.</summary>
    [Export]
    public Camera3D? ScreenCamera { get; set; }

    /// <summary>
    ///     The 3D monitor. Its mesh must be a QuadMesh, which faces local +Z.
    /// </summary>
    [Export]
    public MeshInstance3D? ScreenQuad { get; set; }

    /// <summary>
    ///     The SubViewport that the screen shows; forwarded events are pushed
    ///     into it.
    /// </summary>
    [Export]
    public SubViewport? Viewport { get; set; }

    /// <summary>
    ///     Forwarding happens only while this is true, so the pointer is off
    ///     when the PC is not in use.
    /// </summary>
    public bool Active { get; set; }

    /// <summary>
    ///     The SubViewport pixel of the last forwarded event, for tests.
    /// </summary>
    internal Vector2? LastForwardedPixel { get; private set; }

    /// <summary>
    ///     Intersects a ray with a quad that lies in its local X/Y plane and
    ///     faces local +Z (the QuadMesh default).
    ///     Returns the viewport pixel of the hit point (0,0 at the top-left,
    ///     viewport size at the bottom-right).
    ///     Returns false when the ray is parallel to the quad, hits its back
    ///     face, points away from it, or misses it.
    /// </summary>
    public static bool TryMapToPixel(
        Vector3 rayOrigin,
        Vector3 rayDirection,
        Transform3D quadTransform,
        Vector2 quadSize,
        Vector2I viewportSize,
        out Vector2 pixel)
    {
        pixel = Vector2.Zero;

        Vector3 localOrigin = quadTransform.AffineInverse() * rayOrigin;
        Vector3 localDirection = quadTransform.Basis.Inverse() * rayDirection;

        if (localDirection.Z >= -ParallelTolerance)
            return false;

        float distance = -localOrigin.Z / localDirection.Z;
        if (distance < 0f)
            return false;

        Vector3 hit = localOrigin + localDirection * distance;
        if (Mathf.Abs(hit.X) > quadSize.X / 2f
            || Mathf.Abs(hit.Y) > quadSize.Y / 2f)
            return false;

        pixel = new Vector2(
            (hit.X / quadSize.X + 0.5f) * viewportSize.X,
            (0.5f - hit.Y / quadSize.Y) * viewportSize.Y);
        return true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Active || @event is not InputEventMouse mouse)
            return;
        if (ScreenCamera == null || Viewport == null || ScreenQuad == null)
            return;
        if (ScreenQuad.Mesh is not QuadMesh quad)
            return;

        Vector2 screenPosition = mouse.Position;
        Vector3 origin = ScreenCamera.ProjectRayOrigin(screenPosition);
        Vector3 direction = ScreenCamera.ProjectRayNormal(screenPosition);
        if (!TryMapToPixel(
                origin,
                direction,
                ScreenQuad.GlobalTransform,
                quad.Size,
                Viewport.Size,
                out Vector2 pixel))
            return;

        var forwarded = (InputEventMouse)mouse.Duplicate();
        forwarded.Position = pixel;
        forwarded.GlobalPosition = pixel;
        Viewport.PushInput(forwarded, true);
        LastForwardedPixel = pixel;
        GetViewport().SetInputAsHandled();
    }
}
