using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Turns the shop's furniture records into the JSON text a save slot holds, and back.</summary>
public static class FurnitureSaveCodec
{
    public static string Encode(IReadOnlyList<FurnitureRecord> records) => SaveExtension.SerializeObject(records);

    /// <summary>
    ///     The records in <paramref name="json" />, or an empty list when the text is missing, empty or not a JSON
    ///     list. A list that cannot be read as a whole logs a warning. A record inside it that cannot be read (wrong
    ///     type, no id, null) logs an error naming its index and is skipped, so it never costs the other records.
    /// </summary>
    public static IReadOnlyList<FurnitureRecord> Decode(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        JArray entries;
        try
        {
            entries = JArray.Parse(json);
        }
        catch (JsonException)
        {
            ILog.Warning("Furniture save is unreadable");
            return [];
        }

        var serializer = JsonSerializer.Create(SaveExtension.Settings);
        var records = new List<FurnitureRecord>();
        for (var index = 0; index < entries.Count; index++)
        {
            var record = TryRead(entries[index], serializer);
            if (record == null)
                ILog.Error($"Furniture record {index} is unreadable; skipped");
            else
                records.Add(record);
        }

        return records;
    }

    private static FurnitureRecord? TryRead(JToken entry, JsonSerializer serializer)
    {
        try
        {
            var record = entry.ToObject<FurnitureRecord>(serializer);
            return string.IsNullOrEmpty(record?.Id) ? null : record;
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }
}
