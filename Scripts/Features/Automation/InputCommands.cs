using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Raw input: every command builds the same InputEvent a real device would and feeds it through
///     <c>Input.ParseInputEvent</c>, so the game's own handlers, actions and GUI routing all run.
/// </summary>
public static class InputCommands
{
    private const int DefaultHoldFrames = 2;

    public static void Register(AutomationCommands commands)
    {
        var pointer = commands.Mouse;
        commands.Add(new AutomationCommand("key",
            "{key: \"E\"|\"Escape\"|\"Space\"..., mode?: tap|down|up, frames?: n held for tap, shift?, ctrl?, alt?}",
            args => Key(commands, args)));
        commands.Add(new AutomationCommand("action", "{name: input action, mode?: tap|down|up, frames?: n}",
            args => Action(commands, args)));
        commands.Add(new AutomationCommand("type", "{text} types each character as a key press with its unicode",
            args => TypeText(commands, args)));
        commands.Add(new AutomationCommand("mouse_move",
            "{dx, dy} relative motion (mouse look), or {x, y} moves the pointer to a viewport position",
            args => pointer.Move(commands, args)));
        commands.Add(new AutomationCommand("click",
            "{x?, y?, button?: left|right|middle, double?} presses and releases at a position (default: pointer)",
            args => pointer.Click(commands, args)));
        commands.Add(new AutomationCommand("wheel", "{steps: +up / -down, x?, y?} one press and release per step",
            args => pointer.Wheel(commands, args)));
    }

    private static async Task<JsonNode?> Key(AutomationCommands commands, JsonObject args)
    {
        var name = args.RequiredString("key");
        var keycode = OS.FindKeycodeFromString(name);
        if (keycode == Godot.Key.None)
            throw new AutomationException($"unknown key \"{name}\"");
        var mode = args.OptionalString("mode") ?? "tap";
        await Press(commands, mode, (int)args.Number("frames", DefaultHoldFrames),
            pressed => KeyEvent(keycode, pressed, args));
        return new JsonObject { ["key"] = OS.GetKeycodeString(keycode), ["mode"] = mode };
    }

    private static async Task<JsonNode?> Action(AutomationCommands commands, JsonObject args)
    {
        var name = args.RequiredString("name");
        if (!InputMap.HasAction(name))
            throw new AutomationException($"no input action \"{name}\"");
        var mode = args.OptionalString("mode") ?? "tap";
        await Press(commands, mode, (int)args.Number("frames", DefaultHoldFrames),
            pressed => new InputEventAction { Action = name, Pressed = pressed, Strength = pressed ? 1f : 0f });
        return new JsonObject { ["action"] = name, ["mode"] = mode };
    }

    private static async Task<JsonNode?> TypeText(AutomationCommands commands, JsonObject args)
    {
        var text = args.RequiredString("text");
        foreach (var c in text)
        {
            var keycode = char.IsAsciiLetter(c) ? (Key)char.ToUpperInvariant(c) : (Key)c;
            foreach (var pressed in new[] { true, false })
            {
                Input.ParseInputEvent(new InputEventKey
                    { Keycode = keycode, PhysicalKeycode = keycode, Unicode = c, Pressed = pressed });
                await commands.NextFrame();
            }
        }

        return text.Length;
    }

    /// <summary>tap = down, hold for <paramref name="frames" />, up; down and up send one edge.</summary>
    public static async Task Press(AutomationCommands commands, string mode, int frames,
        System.Func<bool, InputEvent> build)
    {
        if (mode is not ("tap" or "down" or "up"))
            throw new AutomationException($"mode must be tap, down or up, not \"{mode}\"");
        if (mode != "up")
            Input.ParseInputEvent(build(true));
        if (mode == "tap")
            await commands.Frames(System.Math.Max(1, frames));
        if (mode != "down")
            Input.ParseInputEvent(build(false));
        await commands.NextFrame();
    }

    private static InputEventKey KeyEvent(Key keycode, bool pressed, JsonObject args)
    {
        var key = new InputEventKey
        {
            Keycode = keycode, PhysicalKeycode = keycode, Pressed = pressed,
            ShiftPressed = args.Flag("shift"), CtrlPressed = args.Flag("ctrl"), AltPressed = args.Flag("alt")
        };
        var label = OS.GetKeycodeString(keycode);
        if (pressed && label.Length == 1)
            key.Unicode = args.Flag("shift") ? char.ToUpperInvariant(label[0]) : char.ToLowerInvariant(label[0]);
        return key;
    }
}
