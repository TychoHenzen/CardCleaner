using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>The controls a player can see and read, and pressing one of them by its text.</summary>
public static class UiList
{
    public static JsonArray Describe(SceneTree tree, bool all)
    {
        var list = new JsonArray();
        foreach (var control in Controls(tree).Where(c => all || Textual(c) != null))
            list.Add(Entry(control, tree));
        return list;
    }

    public static async Task<JsonNode?> Press(AutomationCommands commands, JsonObject args)
    {
        var button = Find(commands.Tree, args);
        if (button.Disabled)
            throw new AutomationException($"{button.GetPath()} is disabled");
        if (button.GetViewport() == commands.Tree.Root)
        {
            var centre = button.GetGlobalRect().GetCenter();
            await commands.Mouse.MoveTo(commands, centre);
            await VirtualPointer.Button(commands, MouseButton.Left, centre, false);
            return new JsonObject { ["clicked"] = button.GetPath().ToString(), ["at"] = VariantJson.ToJson(centre) };
        }

        // A button drawn into a SubViewport (the shop PC screen) is reached through the 3D screen in a real run;
        // pressing it directly keeps this command independent of where that screen is.
        button.EmitSignal(BaseButton.SignalName.Pressed);
        await commands.NextFrame();
        return new JsonObject { ["pressed"] = button.GetPath().ToString(), ["via"] = "signal" };
    }

    private static BaseButton Find(SceneTree tree, JsonObject args)
    {
        if (args.OptionalString("node") is { } path)
            return AutomationView.RequireNode(tree, path) as BaseButton
                   ?? throw new AutomationException($"{path} is not a button");
        var text = args.RequiredString("text");
        var matches = Controls(tree).OfType<BaseButton>()
            .Where(b => string.Equals(Textual(b)?.Trim(), text, System.StringComparison.OrdinalIgnoreCase))
            .ToList();
        return matches.Count switch
        {
            0 => throw new AutomationException($"no visible button reads \"{text}\""),
            1 => matches[0],
            _ => throw new AutomationException($"{matches.Count} buttons read \"{text}\": "
                                               + string.Join(", ", matches.Select(m => m.GetPath())))
        };
    }

    private static IEnumerable<Control> Controls(SceneTree tree) =>
        tree.Root.FindChildren("*", "Control", true, false).OfType<Control>().Where(c => c.IsVisibleInTree());

    private static string? Textual(Control control) =>
        control switch
        {
            Button button => button.Text,
            Label label => label.Text,
            LineEdit edit => edit.Text,
            RichTextLabel rich => rich.GetParsedText(),
            _ => null
        };

    private static JsonObject Entry(Control control, SceneTree tree)
    {
        var entry = new JsonObject
        {
            ["node"] = control.GetPath().ToString(),
            ["type"] = control.GetClass(),
            ["text"] = Textual(control),
            ["rect"] = VariantJson.ToJson(new Godot.Collections.Array
                { control.GetGlobalRect().Position, control.GetGlobalRect().Size })
        };
        if (control is BaseButton { Disabled: true })
            entry["disabled"] = true;
        if (control.GetViewport() != tree.Root)
            entry["viewport"] = control.GetViewport().GetPath().ToString();
        return entry;
    }
}
