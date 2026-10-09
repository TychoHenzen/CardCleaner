using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Saveable.Extensions;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
/// Converts the loosely typed card entries produced by the save system (dictionaries or Newtonsoft
/// JObjects) into <see cref="SavedCardRecord"/> values.
/// </summary>
internal static class SavedCardReader
{
    /// <summary>
    /// Reads one saved card entry. Returns null (after logging) when the entry cannot be converted;
    /// throws when a converted entry is missing fields or holds malformed values.
    /// </summary>
    internal static SavedCardRecord? Read(object? cardDataObj)
    {
        var cardData = ToDictionary(cardDataObj);
        if (cardData == null)
            return null;

        return new SavedCardRecord(
            new CardSignature(GetFloatArrayFromData(cardData["signature_elements"])),
            GetVector3FromData(cardData["position"]),
            GetVector3FromData(cardData["rotation"]),
            Convert.ToBoolean(cardData["isHeld"]),
            cardData["parentPath"]?.ToString() ?? "");
    }

    // Handle both Dictionary<string, object> and JObject (from Newtonsoft.Json)
    private static Dictionary<string, object>? ToDictionary(object? cardDataObj)
    {
        if (cardDataObj is Dictionary<string, object> dict)
            return dict;

        if (cardDataObj?.GetType().Name == "JObject")
        {
            // Convert JObject to Dictionary using the Saveable plugin's method
            try
            {
                var json = SaveExtension.SerializeObject(cardDataObj);
                var converted = SaveExtension.DeserializeObject<Dictionary<string, object>>(json);
                if (converted != null)
                    return converted;
            }
            catch (Exception ex)
            {
                ILog.Error($"Failed to convert JObject to Dictionary: {ex.Message}");
                return null;
            }
        }

        ILog.Print($"Could not convert card data object, skipping");
        return null;
    }

    private static Vector3 GetVector3FromData(object vectorData)
    {
        if (vectorData is Vector3 vec)
            return vec;

        // Handle JSON object with x, y, z properties
        var json = SaveExtension.SerializeObject(vectorData);
        return SaveExtension.DeserializeObject<Vector3>(json);
    }

    private static float[] GetFloatArrayFromData(object arrayData)
    {
        if (arrayData is float[] arr)
            return arr;

        // Handle JSON array
        var json = SaveExtension.SerializeObject(arrayData);
        return SaveExtension.DeserializeObject<float[]>(json) ?? [];
    }
}
