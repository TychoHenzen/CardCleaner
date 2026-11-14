using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     The automation run's mouse. There is no real pointer on the hidden desktop, so this keeps the position the
///     last event reported and builds every motion and button event from it.
/// </summary>
public sealed class VirtualPointer
{
    private Vector2? _position;

    public Vector2 Position(AutomationCommands commands) =>
        _position ?? commands.Tree.Root.GetVisibleRect().Size / 2;

    public async Task<JsonNode?> Move(AutomationCommands commands, JsonObject args)
    {
        if (args.OptionalNumber("dx") is { } || args.OptionalNumber("dy") is { })
        {
            Relative(new Vector2((float)args.Number("dx", 0), (float)args.Number("dy", 0)), commands);
            await commands.NextFrame();
            return VariantJson.ToJson(Position(commands));
        }

        await MoveTo(commands, Required(args, commands));
        return VariantJson.ToJson(Position(commands));
    }

    /// <summary>Sends one relative motion, as mouse look reads it, without moving the pointer.</summary>
    public void Relative(Vector2 delta, AutomationCommands commands)
    {
        var at = Position(commands);
        Input.ParseInputEvent(new InputEventMouseMotion
            { Relative = delta, ScreenRelative = delta, Position = at, GlobalPosition = at });
    }

    public async Task MoveTo(AutomationCommands commands, Vector2 to)
    {
        var from = Position(commands);
        _position = to;
        Input.ParseInputEvent(new InputEventMouseMotion
            { Position = to, GlobalPosition = to, Relative = to - from, ScreenRelative = to - from });
        await commands.NextFrame();
    }

    public async Task<JsonNode?> Click(AutomationCommands commands, JsonObject args)
    {
        var at = Optional(args) ?? Position(commands);
        var button = ParseButton(args.OptionalString("button") ?? "left");
        await MoveTo(commands, at);
        await Button(commands, button, at, args.Flag("double"));
        return new JsonObject { ["at"] = VariantJson.ToJson(at), ["button"] = button.ToString() };
    }

    public async Task<JsonNode?> Wheel(AutomationCommands commands, JsonObject args)
    {
        var steps = (int)args.Number("steps", 1);
        var at = Optional(args) ?? Position(commands);
        var button = steps >= 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
        for (var i = 0; i < System.Math.Abs(steps); i++)
            await Button(commands, button, at, false);
        return steps;
    }

    /// <summary>A press and a release a frame apart, the way the engine reports a real click.</summary>
    public static async Task Button(AutomationCommands commands, MouseButton button, Vector2 at, bool doubleClick)
    {
        foreach (var pressed in new[] { true, false })
        {
            Input.ParseInputEvent(new InputEventMouseButton
            {
                ButtonIndex = button, Pressed = pressed, Position = at, GlobalPosition = at,
                DoubleClick = doubleClick && pressed, Factor = 1f,
                ButtonMask = pressed ? (MouseButtonMask)(1 << ((int)button - 1)) : 0
            });
            await commands.NextFrame();
        }
    }

    private static MouseButton ParseButton(string name) =>
        name switch
        {
            "left" => MouseButton.Left,
            "right" => MouseButton.Right,
            "middle" => MouseButton.Middle,
            _ => throw new AutomationException($"button must be left, right or middle, not \"{name}\"")
        };

    private static Vector2? Optional(JsonObject args) =>
        args.OptionalNumber("x") is { } x && args.OptionalNumber("y") is { } y ? new Vector2((float)x, (float)y) : null;

    private static Vector2 Required(JsonObject args, AutomationCommands commands) =>
        Optional(args) ?? throw new AutomationException("give {dx, dy} or {x, y}");
}
