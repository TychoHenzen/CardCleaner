using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>Session commands: liveness, waiting, scene changes, a state summary and quitting.</summary>
public static class CoreCommands
{
    private const double MaxWaitSeconds = 600;

    public static void Register(AutomationCommands commands)
    {
        commands.Add(new AutomationCommand("ping", "-> frame, fps, scene", _ => Task.FromResult<JsonNode?>(Ping(commands))));
        commands.Add(new AutomationCommand("wait", "{frames?: n, seconds?: s} waits; seconds are real time",
            args => Wait(commands, args)));
        commands.Add(new AutomationCommand("scene", "{path: res://...tscn} changes scene and waits until it is ready",
            args => ChangeScene(commands, args)));
        commands.Add(new AutomationCommand("state",
            "-> scene, paused, mouse mode, window size, player position and view, focused control",
            _ => Task.FromResult<JsonNode?>(State(commands))));
        commands.Add(new AutomationCommand("pause", "{paused: bool} pauses or resumes the scene tree",
            args => SetPaused(commands, args)));
        commands.Add(new AutomationCommand("quit", "{code?: n} quits the game after replying", args => Quit(commands, args)));
    }

    private static JsonNode Ping(AutomationCommands commands) => new JsonObject
    {
        ["frame"] = Engine.GetProcessFrames(),
        ["fps"] = Engine.GetFramesPerSecond(),
        ["scene"] = commands.Tree.CurrentScene?.SceneFilePath
    };

    private static async Task<JsonNode?> Wait(AutomationCommands commands, JsonObject args)
    {
        var frames = (int)args.Number("frames", 0);
        var seconds = System.Math.Clamp(args.Number("seconds", 0), 0, MaxWaitSeconds);
        await commands.Frames(frames);
        var until = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (Time.GetTicksMsec() < until)
            await commands.NextFrame();
        return Ping(commands);
    }

    private static async Task<JsonNode?> ChangeScene(AutomationCommands commands, JsonObject args)
    {
        var path = args.RequiredString("path");
        if (!ResourceLoader.Exists(path))
            throw new AutomationException($"no scene at {path}");
        var error = commands.Tree.ChangeSceneToFile(path);
        if (error != Error.Ok)
            throw new AutomationException($"ChangeSceneToFile({path}) returned {error}");
        await commands.Frames(3);
        return Ping(commands);
    }

    private static JsonObject State(AutomationCommands commands)
    {
        var tree = commands.Tree;
        var viewport = tree.Root;
        var state = new JsonObject
        {
            ["scene"] = tree.CurrentScene?.SceneFilePath,
            ["paused"] = tree.Paused,
            ["mouse_mode"] = Input.MouseMode.ToString(),
            ["window"] = VariantJson.ToJson(viewport.Size),
            ["focus"] = VariantJson.ToJson(viewport.GuiGetFocusOwner()),
            ["hovered"] = VariantJson.ToJson(viewport.GuiGetHoveredControl())
        };
        if (AutomationView.FindPlayer(tree) is { } player)
            state["player"] = VariantJson.ToJson(player.GlobalPosition);
        if (viewport.GetCamera3D() is { } camera)
            state["view"] = AutomationView.Describe(camera);
        return state;
    }

    private static async Task<JsonNode?> SetPaused(AutomationCommands commands, JsonObject args)
    {
        commands.Tree.Paused = args.Flag("paused", true);
        await commands.NextFrame();
        return commands.Tree.Paused;
    }

    private static async Task<JsonNode?> Quit(AutomationCommands commands, JsonObject args)
    {
        var code = (int)args.Number("code", 0);
        // Quit on a later frame so this reply reaches the client first.
        commands.Tree.CreateTimer(0.2, processAlways: true, processInPhysics: false, ignoreTimeScale: true)
            .Timeout += () => commands.Tree.Quit(code);
        await commands.NextFrame();
        return code;
    }
}
