using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Symbolic movement built from real input: aiming sends mouse motion, walking holds the forward action and
///     steers with mouse motion along a path from the navigation map, so the player controller does the moving.
/// </summary>
public static class MotionCommands
{
    private const float DefaultToleranceDegrees = 1f;
    private const float UseDistanceMeters = 1.6f;

    public static void Register(AutomationCommands commands)
    {
        var aim = new ViewAim();
        var walker = new PathWalker(aim);
        commands.Add(new AutomationCommand("look", "{yaw?, pitch?, tolerance?} turns the view to absolute degrees "
                                                   + "(yaw 0 looks along -Z, positive left; pitch positive up)",
            args => Look(commands, aim, args)));
        commands.Add(new AutomationCommand("look_at", "{target: [x,y,z] | node: path, tolerance?} aims the camera "
                                                      + "centre at a point; reports what the centre ray hits",
            args => LookAt(commands, aim, args)));
        commands.Add(new AutomationCommand("nav_path", "{target | node} -> the navigation path from the player",
            args => Task.FromResult<JsonNode?>(NavPath(commands, args))));
        commands.Add(new AutomationCommand("walk_to",
            "{target | node, stop?: m (0.5), timeout?: s (30)} walks the player there with forward + mouse look",
            args => walker.Walk(commands, AutomationView.ResolveTarget(commands.Tree, args),
                (float)args.Number("stop", PathWalker.DefaultStopMeters), args.Number("timeout", 30))));
        commands.Add(new AutomationCommand("use",
            "{node, distance?: m (1.6), button?: left} walks near a node, aims at it and clicks (the interact input)",
            args => Use(commands, aim, walker, args)));
    }

    private static async Task<JsonNode?> Look(AutomationCommands commands, ViewAim aim, JsonObject args)
    {
        var now = ViewAim.Angles(commands);
        var yaw = (float)args.Number("yaw", now.X);
        var pitch = (float?)args.OptionalNumber("pitch");
        return await AimAndDescribe(commands, aim, yaw, pitch, args);
    }

    private static async Task<JsonNode?> LookAt(AutomationCommands commands, ViewAim aim, JsonObject args)
    {
        var target = AutomationView.ResolveTarget(commands.Tree, args);
        var camera = AutomationView.RequireCamera(commands.Tree);
        var wanted = AutomationView.YawPitch(target - camera.GlobalPosition);
        return await AimAndDescribe(commands, aim, wanted.X, wanted.Y, args);
    }

    private static async Task<JsonNode?> AimAndDescribe(AutomationCommands commands, ViewAim aim, float yaw,
        float? pitch, JsonObject args)
    {
        var tolerance = (float)args.Number("tolerance", DefaultToleranceDegrees);
        var (_, pitchLimited) = await aim.Aim(commands, yaw, pitch, tolerance);
        var view = AutomationView.Describe(AutomationView.RequireCamera(commands.Tree));
        view["pitch_limited"] = pitchLimited;
        view["gain"] = VariantJson.ToJson(aim.Gain);
        return view;
    }

    private static JsonNode NavPath(AutomationCommands commands, JsonObject args)
    {
        var player = AutomationView.RequirePlayer(commands.Tree);
        var path = PathWalker.PathFor(player, AutomationView.ResolveTarget(commands.Tree, args));
        var length = 0f;
        for (var i = 1; i < path.Length; i++)
            length += path[i - 1].DistanceTo(path[i]);
        var points = new JsonArray();
        foreach (var point in path)
            points.Add(VariantJson.ToJson(point));
        return new JsonObject { ["points"] = points, ["length"] = System.Math.Round(length, 3) };
    }

    private static async Task<JsonNode?> Use(AutomationCommands commands, ViewAim aim, PathWalker walker,
        JsonObject args)
    {
        var node = args.RequiredString("node");
        var target = AutomationView.ResolveTarget(commands.Tree, args);
        var walked = await walker.Walk(commands, target, (float)args.Number("distance", UseDistanceMeters), 30);
        var view = await LookAt(commands, aim, new JsonObject { ["node"] = node });
        var click = await commands.Mouse.Click(commands, new JsonObject { ["button"] = args.OptionalString("button") });
        return new JsonObject { ["walk"] = walked, ["view"] = view, ["click"] = click };
    }
}
