using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CardCleaner.Scripts.Features.Automation;

/// <summary>Typed reads of a request's <c>args</c>; a wrong type is an <see cref="AutomationException" />.</summary>
public static class AutomationArgs
{
    public static string? OptionalString(this JsonObject args, string name) =>
        args[name] switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
            _ => throw new AutomationException($"\"{name}\" must be a string")
        };

    public static string RequiredString(this JsonObject args, string name) =>
        args.OptionalString(name) ?? throw new AutomationException($"\"{name}\" is required");

    public static double? OptionalNumber(this JsonObject args, string name) =>
        args[name] switch
        {
            null => null,
            JsonValue value when value.GetValueKind() == JsonValueKind.Number => value.GetValue<double>(),
            _ => throw new AutomationException($"\"{name}\" must be a number")
        };

    public static double Number(this JsonObject args, string name, double fallback) =>
        args.OptionalNumber(name) ?? fallback;

    public static bool Flag(this JsonObject args, string name, bool fallback = false) =>
        args[name] switch
        {
            null => fallback,
            JsonValue value when value.GetValueKind() is JsonValueKind.True or JsonValueKind.False =>
                value.GetValue<bool>(),
            _ => throw new AutomationException($"\"{name}\" must be true or false")
        };

    /// <summary>Reads <c>[x, y, z]</c> (or <c>[x, y]</c> when <paramref name="length" /> is 2).</summary>
    public static double[]? OptionalNumbers(this JsonObject args, string name, int length)
    {
        if (args[name] is null)
            return null;
        if (args[name] is not JsonArray array || array.Count != length)
            throw new AutomationException($"\"{name}\" must be an array of {length} numbers");
        var numbers = new double[length];
        for (var i = 0; i < length; i++)
        {
            numbers[i] = array[i] is JsonValue v && v.GetValueKind() == JsonValueKind.Number
                ? v.GetValue<double>()
                : throw new AutomationException($"\"{name}\"[{i.ToString(CultureInfo.InvariantCulture)}] "
                                                + "must be a number");
        }

        return numbers;
    }
}
