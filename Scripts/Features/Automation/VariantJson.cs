using System.Text.Json;
using System.Text.Json.Nodes;
using Godot;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>
///     Converts Godot values to JSON for replies, and JSON to the Variant a property or argument expects.
/// </summary>
public static class VariantJson
{
    private const int MaxDepth = 4;

    public static JsonNode? ToJson(Variant value, int depth = 0)
    {
        if (depth > MaxDepth)
            return value.ToString();
        return value.VariantType switch
        {
            Variant.Type.Nil => null,
            Variant.Type.Bool => value.AsBool(),
            Variant.Type.Int => value.AsInt64(),
            Variant.Type.Float => value.AsDouble(),
            Variant.Type.String or Variant.Type.StringName or Variant.Type.NodePath => value.AsString(),
            Variant.Type.Vector2 => Numbers(value.AsVector2().X, value.AsVector2().Y),
            Variant.Type.Vector2I => Numbers(value.AsVector2I().X, value.AsVector2I().Y),
            Variant.Type.Vector3 => Numbers(value.AsVector3().X, value.AsVector3().Y, value.AsVector3().Z),
            Variant.Type.Vector3I => Numbers(value.AsVector3I().X, value.AsVector3I().Y, value.AsVector3I().Z),
            Variant.Type.Color => Numbers(value.AsColor().R, value.AsColor().G, value.AsColor().B, value.AsColor().A),
            Variant.Type.Object => ObjectJson(value.AsGodotObject()),
            Variant.Type.Array => ArrayJson(value.AsGodotArray(), depth),
            Variant.Type.Dictionary => DictionaryJson(value.AsGodotDictionary(), depth),
            _ => value.ToString()
        };
    }

    /// <summary>Builds the Variant for <paramref name="type" /> from JSON; Nil means "whatever the JSON is".</summary>
    public static Variant FromJson(JsonNode? json, Variant.Type type = Variant.Type.Nil)
    {
        if (json is null)
            return default;
        if (json is JsonArray array && Vectorish(type))
            return Vector(array, type);
        if (json is JsonArray list)
        {
            var result = new Godot.Collections.Array();
            foreach (var item in list)
                result.Add(FromJson(item));
            return result;
        }

        if (json is JsonObject)
            return Json.ParseString(json.ToJsonString());
        return Scalar((JsonValue)json, type);
    }

    private static Variant Scalar(JsonValue value, Variant.Type type) =>
        value.GetValueKind() switch
        {
            JsonValueKind.True or JsonValueKind.False => value.GetValue<bool>(),
            JsonValueKind.Number when type == Variant.Type.Int => (long)value.GetValue<double>(),
            JsonValueKind.Number => value.GetValue<double>(),
            _ when type == Variant.Type.NodePath => new NodePath(value.GetValue<string>()),
            _ when type == Variant.Type.StringName => new StringName(value.GetValue<string>()),
            _ => value.GetValue<string>()
        };

    private static bool Vectorish(Variant.Type type) =>
        type is Variant.Type.Vector2 or Variant.Type.Vector2I or Variant.Type.Vector3 or Variant.Type.Vector3I
            or Variant.Type.Color;

    private static Variant Vector(JsonArray array, Variant.Type type)
    {
        var n = new float[4];
        for (var i = 0; i < array.Count && i < n.Length; i++)
            n[i] = (float)(array[i]?.GetValue<double>() ?? 0d);
        return type switch
        {
            Variant.Type.Vector2 => new Vector2(n[0], n[1]),
            Variant.Type.Vector2I => new Vector2I((int)n[0], (int)n[1]),
            Variant.Type.Vector3 => new Vector3(n[0], n[1], n[2]),
            Variant.Type.Vector3I => new Vector3I((int)n[0], (int)n[1], (int)n[2]),
            _ => new Color(n[0], n[1], n[2], array.Count > 3 ? n[3] : 1f)
        };
    }

    private static JsonArray Numbers(params double[] values)
    {
        var array = new JsonArray();
        foreach (var v in values)
            array.Add(System.Math.Round(v, 4));
        return array;
    }

    private static JsonNode? ObjectJson(GodotObject? obj) =>
        obj switch
        {
            null => null,
            Node node when node.IsInsideTree() => new JsonObject
                { ["node"] = node.GetPath().ToString(), ["type"] = node.GetClass() },
            Resource resource => new JsonObject
                { ["resource"] = resource.ResourcePath, ["type"] = resource.GetClass() },
            _ => new JsonObject { ["type"] = obj.GetClass() }
        };

    private static JsonArray ArrayJson(Godot.Collections.Array array, int depth)
    {
        var result = new JsonArray();
        foreach (var item in array)
            result.Add(ToJson(item, depth + 1));
        return result;
    }

    private static JsonObject DictionaryJson(Godot.Collections.Dictionary dictionary, int depth)
    {
        var result = new JsonObject();
        foreach (var (key, value) in dictionary)
            result[key.ToString()] = ToJson(value, depth + 1);
        return result;
    }
}
