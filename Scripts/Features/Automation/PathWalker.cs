using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using static System.FormattableString;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Walks the player along a navigation path by holding the forward action and steering with mouse motion. It
///     stops when the player is within the stop distance of the goal, and fails when the player stops making
///     progress, is teleported (the player controller's stuck and out-of-bounds resets do that) or time runs out.
/// </summary>
public sealed class PathWalker(ViewAim aim)
{
    public const float DefaultStopMeters = 0.5f;
    public const string ForwardAction = "ui_up";
    private const float WaypointReachedMeters = 0.35f;
    private const float WalkWhileTurnedDegrees = 35f;
    private const float TeleportMeters = 2.5f;
    private const float ProgressMeters = 0.15f;
    private const double StuckSeconds = 2.5;

    public static Vector3[] PathFor(Node3D player, Vector3 target)
    {
        var map = player.GetWorld3D().NavigationMap;
        var from = NavigationServer3D.MapGetClosestPoint(map, player.GlobalPosition);
        var to = NavigationServer3D.MapGetClosestPoint(map, target);
        var path = NavigationServer3D.MapGetPath(map, from, to, true);
        if (path.Length == 0)
            throw new AutomationException($"no navigation path from {player.GlobalPosition} to {target}");
        return path;
    }

    public async Task<JsonNode?> Walk(AutomationCommands commands, Vector3 target, float stop, double timeout)
    {
        var player = AutomationView.RequirePlayer(commands.Tree);
        var path = PathFor(player, target);
        var started = Time.GetTicksMsec();
        try
        {
            await Follow(commands, player, path, target, stop, started + (ulong)(timeout * 1000));
        }
        finally
        {
            Input.ParseInputEvent(new InputEventAction { Action = ForwardAction, Pressed = false });
        }

        return new JsonObject
        {
            ["position"] = VariantJson.ToJson(player.GlobalPosition),
            ["distance"] = System.Math.Round(Flat(target - player.GlobalPosition).Length(), 3),
            ["seconds"] = (Time.GetTicksMsec() - started) / 1000.0,
            ["waypoints"] = path.Length
        };
    }

    private async Task Follow(AutomationCommands commands, Node3D player, Vector3[] path, Vector3 target, float stop,
        ulong deadline)
    {
        var index = 0;
        var last = player.GlobalPosition;
        var anchor = last;
        var anchorTime = Time.GetTicksMsec();
        while (Flat(target - player.GlobalPosition).Length() > stop)
        {
            var at = player.GlobalPosition;
            if (at.DistanceTo(last) > TeleportMeters)
                throw new AutomationException($"the player was moved from {last} to {at} (a stuck or bounds reset?)");
            if (Time.GetTicksMsec() > deadline)
                throw new AutomationException(
                    Invariant($"timed out at {at}, {Flat(target - at).Length():F2} m from the goal"));
            if (Flat(at - anchor).Length() >= ProgressMeters)
                (anchor, anchorTime) = (at, Time.GetTicksMsec());
            else if (Time.GetTicksMsec() - anchorTime > StuckSeconds * 1000)
                throw new AutomationException(Invariant($"blocked at {at}: no progress for {StuckSeconds} s"));

            while (index < path.Length - 1 && Flat(path[index] - at).Length() < WaypointReachedMeters)
                index++;
            await Steer(commands, at, index < path.Length - 1 ? path[index] : target);
            last = at;
        }
    }

    private async Task Steer(AutomationCommands commands, Vector3 at, Vector3 waypoint)
    {
        var before = ViewAim.Angles(commands);
        var wantedYaw = AutomationView.YawPitch(new Vector3(waypoint.X - at.X, 0f, waypoint.Z - at.Z)).X;
        var error = AutomationView.AngleDelta(before.X, wantedYaw);
        var pixels = aim.PixelsFor(new Vector2(error, 0f));
        if (Mathf.Abs(pixels.X) >= 1f)
            commands.Mouse.Relative(pixels, commands);
        Input.ParseInputEvent(new InputEventAction
            { Action = ForwardAction, Pressed = Mathf.Abs(error) < WalkWhileTurnedDegrees, Strength = 1f });
        await commands.NextFrame();
        var turned = AutomationView.AngleDelta(before.X, ViewAim.Angles(commands).X);
        aim.Learn(pixels, new Vector2(turned, 0f));
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);
}
