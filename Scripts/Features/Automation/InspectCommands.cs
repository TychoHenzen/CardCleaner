using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>Reads and pokes the scene tree: node listings, properties, method calls and on-screen controls.</summary>
public static class InspectCommands
{
    private const int DefaultDepth = 2;
    private const int MaxFound = 200;

    public static void Register(AutomationCommands commands)
    {
        var tree = commands.Tree;
        commands.Add(new AutomationCommand("tree", "{node?: path (scene root), depth?: n (2)} -> names, types, children",
            args => Done(Tree(AutomationView.RequireNode(tree, args.OptionalString("node") ?? "."),
                (int)args.Number("depth", DefaultDepth)))));
        commands.Add(new AutomationCommand("find", "{group? | type? | name?: glob} -> matching node paths",
            args => Done(Find(tree, args))));
        commands.Add(new AutomationCommand("get", "{node, property} -> the property value",
            args => Done(VariantJson.ToJson(AutomationView.RequireNode(tree, args.RequiredString("node"))
                .GetIndexed(args.RequiredString("property"))))));
        commands.Add(new AutomationCommand("set", "{node, property, value} sets a property (vectors as arrays)",
            args => Done(Set(tree, args))));
        commands.Add(new AutomationCommand("call", "{node, method, args?: []} calls a method -> its return value",
            args => Done(Call(tree, args))));
        commands.Add(new AutomationCommand("ui", "{all?: bool} -> visible buttons, labels and text fields with rects",
            args => Done(UiList.Describe(tree, args.Flag("all")))));
        commands.Add(new AutomationCommand("press",
            "{text | node} clicks a visible button at its centre (signal \"pressed\" inside a SubViewport)",
            args => UiList.Press(commands, args)));
    }

    private static Task<JsonNode?> Done(JsonNode? result) => Task.FromResult(result);

    private static JsonObject Tree(Node node, int depth)
    {
        var entry = new JsonObject { ["name"] = node.Name.ToString(), ["type"] = node.GetClass() };
        if (node.GetScript().AsGodotObject() is Script script)
            entry["script"] = script.ResourcePath;
        if (node is Node3D spatial)
            entry["position"] = VariantJson.ToJson(spatial.GlobalPosition);
        if (node.GetChildCount() == 0)
            return entry;
        if (depth <= 0)
        {
            entry["children"] = node.GetChildCount();
            return entry;
        }

        var children = new JsonArray();
        foreach (var child in node.GetChildren())
            children.Add(Tree(child, depth - 1));
        entry["children"] = children;
        return entry;
    }

    private static JsonArray Find(SceneTree tree, JsonObject args)
    {
        var group = args.OptionalString("group");
        var type = args.OptionalString("type");
        var name = args.OptionalString("name");
        var nodes = group != null
            ? tree.GetNodesInGroup(group).AsEnumerable()
            : tree.Root.FindChildren(name ?? "*", type ?? "", true, false).AsEnumerable();
        var found = new JsonArray();
        foreach (var node in nodes.Where(n => type == null || n.IsClass(type) || ScriptNamed(n, type)).Take(MaxFound))
            found.Add(node.GetPath().ToString());
        return found;
    }

    private static bool ScriptNamed(Node node, string type) =>
        node.GetScript().AsGodotObject() is Script script
        && System.IO.Path.GetFileNameWithoutExtension(script.ResourcePath) == type;

    private static JsonNode? Set(SceneTree tree, JsonObject args)
    {
        var node = AutomationView.RequireNode(tree, args.RequiredString("node"));
        var property = args.RequiredString("property");
        var current = node.GetIndexed(property);
        node.SetIndexed(property, VariantJson.FromJson(args["value"], current.VariantType));
        return VariantJson.ToJson(node.GetIndexed(property));
    }

    private static JsonNode? Call(SceneTree tree, JsonObject args)
    {
        var node = AutomationView.RequireNode(tree, args.RequiredString("node"));
        var method = args.RequiredString("method");
        if (!node.HasMethod(method))
            throw new AutomationException($"{node.GetPath()} has no method {method}");
        var callArgs = args["args"] is JsonArray list
            ? list.Select(item => VariantJson.FromJson(item)).ToArray()
            : [];
        return VariantJson.ToJson(node.Call(method, callArgs));
    }
}
