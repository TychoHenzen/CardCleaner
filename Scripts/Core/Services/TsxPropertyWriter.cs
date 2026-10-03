using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Represents a typed property for TSX files.
/// </summary>
public class TsxProperty
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public string Value { get; set; } = "";
    /// <summary>
    /// Custom property type name (for enums defined in .tiled-project).
    /// When set, the property will include propertytype="X" attribute.
    /// </summary>
    public string? PropertyType { get; set; }

    public TsxProperty() { }

    public TsxProperty(string name, string type, string value, string? propertyType = null)
    {
        Name = name;
        Type = type;
        Value = value;
        PropertyType = propertyType;
    }

    // Factory methods for type-safe property creation
    public static TsxProperty String(string name, string value) => new(name, "string", value);
    public static TsxProperty Int(string name, int value) => new(name, "int", value.ToString());
    public static TsxProperty Float(string name, float value) =>
        new(name, "float", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static TsxProperty Bool(string name, bool value) => new(name, "bool", value.ToString().ToLowerInvariant());

    /// <summary>
    /// Creates a property with a custom enum type (string storage).
    /// </summary>
    public static TsxProperty StringEnum(string name, string propertyType, string value) =>
        new(name, "string", value, propertyType);

    /// <summary>
    /// Creates a property with a custom enum type (int storage, for flags).
    /// </summary>
    public static TsxProperty IntEnum(string name, string propertyType, int value) =>
        new(name, "int", value.ToString(), propertyType);

    // Value accessors
    public string AsString() => Value;
    public int AsInt() => int.TryParse(Value, out var v) ? v : 0;
    public float AsFloat() => float.TryParse(Value, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
    public bool AsBool() => Value.ToLowerInvariant() is "true" or "1";
}
