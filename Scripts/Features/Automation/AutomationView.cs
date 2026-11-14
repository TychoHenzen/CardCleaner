using System.Linq;
using System.Text.Json.Nodes;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>Finds the player and describes or aims the active camera; shared by the state and motion commands.</summary>
public static class AutomationView
{
    public const string PlayerGroup = "player";
    private const float LookRayMeters = 50f;

    public static Node3D? FindPlayer(SceneTree tree) =>
        tree.GetNodesInGroup(PlayerGroup).OfType<Node3D>().FirstOrDefault(n => n.IsInsideTree());

    public static Node3D RequirePlayer(SceneTree tree) =>
        FindPlayer(tree) ?? throw new AutomationException($"no node in the \"{PlayerGroup}\" group");

    public static Camera3D RequireCamera(SceneTree tree) =>
        tree.Root.GetCamera3D() ?? throw new AutomationException("no current 3D camera");

    /// <summary>Yaw in degrees (0 looks along -Z, positive turns left) and pitch in degrees (positive looks up).</summary>
    public static Vector2 YawPitch(Vector3 forward)
    {
        var dir = forward.Normalized();
        var yaw = Mathf.RadToDeg(Mathf.Atan2(-dir.X, -dir.Z));
        var pitch = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(dir.Y, -1f, 1f)));
        return new Vector2(yaw, pitch);
    }

    public static Vector3 Forward(Camera3D camera) => -camera.GlobalBasis.Z;

    /// <summary>The signed smallest angle from <paramref name="from" /> to <paramref name="to" />, in degrees.</summary>
    public static float AngleDelta(float from, float to) => Mathf.Wrap(to - from, -180f, 180f);

    public static JsonObject Describe(Camera3D camera)
    {
        var angles = YawPitch(Forward(camera));
        var view = new JsonObject
        {
            ["camera"] = VariantJson.ToJson(camera.GlobalPosition),
            ["yaw"] = System.Math.Round(angles.X, 2),
            ["pitch"] = System.Math.Round(angles.Y, 2)
        };
        if (LookHit(camera) is { } hit)
        {
            view["looking_at"] = VariantJson.ToJson(hit["collider"]);
            view["hit"] = VariantJson.ToJson(hit["position"]);
        }

        return view;
    }

    /// <summary>The first physics body on the camera's centre ray, as the raw IntersectRay dictionary.</summary>
    public static Godot.Collections.Dictionary? LookHit(Camera3D camera, uint mask = uint.MaxValue)
    {
        var from = camera.GlobalPosition;
        var query = PhysicsRayQueryParameters3D.Create(from, from + Forward(camera) * LookRayMeters, mask);
        query.CollideWithAreas = true;
        // The camera sits inside the player's own body; skip every body the camera hangs under.
        var exclude = new Godot.Collections.Array<Rid>();
        for (var node = camera.GetParent(); node != null; node = node.GetParent())
        {
            if (node is CollisionObject3D body)
                exclude.Add(body.GetRid());
        }

        query.Exclude = exclude;
        var hit = camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        return hit.Count == 0 ? null : hit;
    }

    /// <summary>
    ///     The point a command aims at: <c>target: [x, y, z]</c>, or the global position of <c>node</c> (a path from
    ///     the root or the current scene).
    /// </summary>
    public static Vector3 ResolveTarget(SceneTree tree, JsonObject args)
    {
        if (args.OptionalNumbers("target", 3) is { } xyz)
            return new Vector3((float)xyz[0], (float)xyz[1], (float)xyz[2]);
        var path = args.OptionalString("node")
                   ?? throw new AutomationException("give \"target\": [x, y, z] or \"node\": path");
        return ResolveNode(tree, path) switch
        {
            Node3D spatial => spatial.GlobalPosition,
            { } other => throw new AutomationException($"{path} is a {other.GetClass()}, not a Node3D"),
            null => throw new AutomationException($"no node at {path}")
        };
    }

    /// <summary>Resolves an absolute path, a path relative to the current scene, or a unique <c>%Name</c>.</summary>
    public static Node? ResolveNode(SceneTree tree, string path)
    {
        if (path.StartsWith('/'))
            return tree.Root.GetNodeOrNull(path);
        var scene = tree.CurrentScene;
        if (scene == null)
            return tree.Root.GetNodeOrNull(path);
        if (path == ".")
            return scene;
        return scene.GetNodeOrNull(path) ?? tree.Root.GetNodeOrNull(path) ?? scene.FindChild(path, true, false);
    }

    public static Node RequireNode(SceneTree tree, string path) =>
        ResolveNode(tree, path) ?? throw new AutomationException($"no node at {path}");
}
