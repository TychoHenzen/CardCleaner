using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Maps auto-tile borders to their compiled atlas coordinates.
/// Used at runtime to look up the correct composite tile for terrain transitions.
///
/// Key format: "{border_id}|{outer_terrain}" where:
/// - border_id: The auto-tile's tile ID (e.g., "grass_border")
/// - outer_terrain: The background terrain ID (e.g., "sand") or "*" for compositable's direct lookup
///
/// For compositable tiles, entries are generated for each base terrain during atlas compilation.
/// For fixed tiles, a single entry exists with the baked outer terrain.
/// </summary>
public class CompiledTransitionMap
{
    /// <summary>
    /// Version for forward compatibility
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Map of transition keys to their atlas coordinate entries.
    /// Key format: "{border_id}|{outer_terrain}"
    /// </summary>
    [JsonPropertyName("transitions")]
    public Dictionary<string, TransitionEntry> Transitions { get; set; } = new();

    /// <summary>
    /// Creates a lookup key for the transition map.
    /// </summary>
    public static string CreateKey(string borderId, string outerTerrain)
    {
        return $"{borderId}|{outerTerrain}";
    }

    /// <summary>
    /// Parses a lookup key back into its components.
    /// </summary>
    public static (string borderId, string outerTerrain) ParseKey(string key)
    {
        var parts = key.Split('|', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : (key, "");
    }

    /// <summary>
    /// Adds a transition entry for the given border and outer terrain.
    /// </summary>
    public void AddTransition(string borderId, string outerTerrain, AutoTileFormat format, Vector2I[] variants)
    {
        var key = CreateKey(borderId, outerTerrain);
        Transitions[key] = new TransitionEntry
        {
            Format = format.ToString().ToLowerInvariant(),
            Variants = ConvertToVariantData(variants)
        };
    }

    /// <summary>
    /// Looks up the atlas coordinates for a specific transition and bitmask.
    /// Returns null if the transition is not found.
    /// </summary>
    public Vector2I? GetVariantCoords(string borderId, string outerTerrain, int bitmask)
    {
        var key = CreateKey(borderId, outerTerrain);
        if (!Transitions.TryGetValue(key, out var entry))
            return null;

        // Convert bitmask to variant index based on format
        var index = entry.Format == "blob47"
            ? NeighborBitmask8.GetBlobIndex(bitmask)
            : bitmask; // Corner16/Edge16 use bitmask directly as index

        if (index < 0 || index >= entry.Variants.Length)
            return null;

        var variant = entry.Variants[index];
        return variant != null ? new Vector2I(variant.X, variant.Y) : null;
    }

    /// <summary>
    /// Gets all transitions for a specific border (all outer terrain combinations).
    /// </summary>
    public IEnumerable<(string outerTerrain, TransitionEntry entry)> GetTransitionsForBorder(string borderId)
    {
        var prefix = $"{borderId}|";
        foreach (var (key, entry) in Transitions)
        {
            if (key.StartsWith(prefix))
            {
                var outerTerrain = key[prefix.Length..];
                yield return (outerTerrain, entry);
            }
        }
    }

    /// <summary>
    /// Gets all unique border IDs in the map.
    /// </summary>
    public IEnumerable<string> GetAllBorderIds()
    {
        var seen = new HashSet<string>();
        foreach (var key in Transitions.Keys)
        {
            var (borderId, _) = ParseKey(key);
            if (seen.Add(borderId))
                yield return borderId;
        }
    }

    private static VariantCoord?[] ConvertToVariantData(Vector2I[] variants)
    {
        var result = new VariantCoord?[variants.Length];
        for (var i = 0; i < variants.Length; i++)
        {
            result[i] = new VariantCoord { X = variants[i].X, Y = variants[i].Y };
        }
        return result;
    }
}

/// <summary>
/// A single transition entry containing variant coordinates for all bitmask values.
/// </summary>
public class TransitionEntry
{
    /// <summary>
    /// Auto-tile format: "corner16", "edge16", or "blob47"
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; } = "corner16";

    /// <summary>
    /// Atlas coordinates for each variant index.
    /// For Corner16/Edge16: 16 entries (index = bitmask 0-15)
    /// For Blob47: 47 entries (index = blob index 0-46)
    /// Null entries mean "use base tile coords" (shouldn't happen for compiled tiles).
    /// </summary>
    [JsonPropertyName("variants")]
    public VariantCoord?[] Variants { get; set; } = [];
}

/// <summary>
/// JSON-serializable 2D coordinate.
/// </summary>
public class VariantCoord
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}
