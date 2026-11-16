using Godot;

namespace CardCleaner.Scripts.Core.Utilities;

/// <summary>Helpers for physics rays cast along a camera's view.</summary>
public static class CameraRay
{
    /// <summary>
    ///     Every collision body the camera hangs under, such as the player's own capsule. The player's camera sits
    ///     on the top of that capsule, so a ray cast from it would hit the player first unless these are excluded.
    /// </summary>
    public static Godot.Collections.Array<Rid> OwnBodies(Camera3D camera)
    {
        var bodies = new Godot.Collections.Array<Rid>();
        for (var node = camera.GetParent(); node != null; node = node.GetParent())
        {
            if (node is CollisionObject3D body)
                bodies.Add(body.GetRid());
        }

        return bodies;
    }
}
