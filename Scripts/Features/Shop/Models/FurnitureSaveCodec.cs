using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Newtonsoft.Json;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Turns the shop's furniture records into the JSON text a save slot holds, and back.</summary>
public static class FurnitureSaveCodec
{
    public static string Encode(IReadOnlyList<FurnitureRecord> records) => SaveExtension.SerializeObject(records);

    /// <summary>
    ///     The records in <paramref name="json" />, or an empty list when the text is missing, empty or not a
    ///     list of furniture records. A malformed text logs a warning and loads nothing rather than failing the load.
    /// </summary>
    public static IReadOnlyList<FurnitureRecord> Decode(string? json)
    {
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            var records = SaveExtension.DeserializeObject<List<FurnitureRecord?>>(json);
            // ASSUMPTION: a null entry in the list is malformed, so it is dropped, not failed (Risk Low).
            return records?.OfType<FurnitureRecord>().ToList() ?? [];
        }
        catch (JsonException)
        {
            ILog.Warning("Furniture save is unreadable");
            return [];
        }
    }
}
