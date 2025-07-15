using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Core.Services;

public class Vector2IJsonConverter : JsonConverter<Vector2I>
{
    public override Vector2I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array for Vector2I");

        reader.Read();
        var x = reader.GetInt32();
        reader.Read();
        var y = reader.GetInt32();
        reader.Read(); // End array

        return new Vector2I(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Vector2I value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

public class Vector3IJsonConverter : JsonConverter<Vector3I>
{
    public override Vector3I Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected array for Vector3I");

        reader.Read();
        var x = reader.GetInt32();
        reader.Read();
        var y = reader.GetInt32();
        reader.Read();
        var z = reader.GetInt32();
        reader.Read(); // End array

        return new Vector3I(x, y, z);
    }

    public override void Write(Utf8JsonWriter writer, Vector3I value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteEndArray();
    }
}

public class GodotArrayJsonConverter<[MustBeVariant] T> : JsonConverter<Array<T>>
{
    public override Array<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var array = new Array<T>();

        if (reader.TokenType != JsonTokenType.StartArray)
            return array;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var item = JsonSerializer.Deserialize<T>(ref reader, options);
            if (item != null)
                array.Add(item);
        }

        return array;
    }

    public override void Write(Utf8JsonWriter writer, Array<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value) JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}

public class CompatibilityTagArrayJsonConverter : JsonConverter<Array<CompatibilityTag>>
{
    public override Array<CompatibilityTag>? Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        // During reading, this will be handled by the string array property
        // This converter is here to prevent serialization of the object array
        reader.Skip();
        return null;
    }

    public override void Write(Utf8JsonWriter writer, Array<CompatibilityTag> value, JsonSerializerOptions options)
    {
        // Don't write object arrays - they should be [JsonIgnore]
        writer.WriteNullValue();
    }
}