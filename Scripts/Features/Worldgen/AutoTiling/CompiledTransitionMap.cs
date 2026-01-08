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
    /// Each bitmask has a single variant (backward compatible).
    /// </summary>
    /// <param name="borderId">The border tile ID.</param>
    /// <param name="outerTerrain">The outer terrain tile ID.</param>
    /// <param name="formatName">The auto-tile format name (e.g., "corner16", "edge16", "blob47").</param>
    /// <param name="variants">Atlas coordinates for each bitmask (single variant per bitmask).</param>
    public void AddTransition(string borderId, string outerTerrain, string formatName, Vector2I[] variants)
    {
        var key = CreateKey(borderId, outerTerrain);
        Transitions[key] = new TransitionEntry
        {
            Format = formatName.ToLowerInvariant(),
            Variants = ConvertToVariantData(variants)
        };
    }

    /// <summary>
    /// Adds a transition entry with multiple variants per bitmask for random selection.
    /// </summary>
    /// <param name="borderId">The border tile ID.</param>
    /// <param name="outerTerrain">The outer terrain tile ID.</param>
    /// <param name="formatName">The auto-tile format name (e.g., "corner16", "edge16", "blob47").</param>
    /// <param name="variantArrays">Atlas coordinates for each bitmask, where each bitmask can have multiple variants.</param>
    public void AddTransitionWithVariants(string borderId, string outerTerrain, string formatName, Vector2I[][] variantArrays)
    {
        var key = CreateKey(borderId, outerTerrain);
        Transitions[key] = new TransitionEntry
        {
            Format = formatName.ToLowerInvariant(),
            Variants = ConvertToVariantData(variantArrays)
        };
    }

    /// <summary>
    /// Looks up the atlas coordinates for a specific transition and bitmask.
    /// When multiple variants exist, returns the first one.
    /// Use GetVariantCoordsWithRandom for random variant selection.
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

        var variants = entry.Variants[index];
        if (variants == null || variants.Length == 0)
            return null;

        // Return first variant (for deterministic behavior)
        return new Vector2I(variants[0].X, variants[0].Y);
    }

    /// <summary>
    /// Looks up the atlas coordinates for a specific transition and bitmask with random variant selection.
    /// Uses position-based seed for consistent re-renders of the same tile.
    /// Returns null if the transition is not found.
    /// </summary>
    public Vector2I? GetVariantCoordsWithRandom(string borderId, string outerTerrain, int bitmask, int positionSeed)
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

        var variants = entry.Variants[index];
        if (variants == null || variants.Length == 0)
            return null;

        // Use position seed to select variant deterministically
        var variantIndex = ((positionSeed % variants.Length) + variants.Length) % variants.Length;
        return new Vector2I(variants[variantIndex].X, variants[variantIndex].Y);
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

    private static VariantCoord[]?[] ConvertToVariantData(Vector2I[] variants)
    {
        var result = new VariantCoord[]?[variants.Length];
        for (var i = 0; i < variants.Length; i++)
        {
            // Wrap each single coordinate in an array (for backward compatibility)
            result[i] = new[] { new VariantCoord { X = variants[i].X, Y = variants[i].Y } };
        }
        return result;
    }

    private static VariantCoord[]?[] ConvertToVariantData(Vector2I[][] variantArrays)
    {
        var result = new VariantCoord[]?[variantArrays.Length];
        for (var i = 0; i < variantArrays.Length; i++)
        {
            if (variantArrays[i] == null || variantArrays[i].Length == 0)
            {
                result[i] = null;
                continue;
            }

            result[i] = new VariantCoord[variantArrays[i].Length];
            for (var j = 0; j < variantArrays[i].Length; j++)
            {
                result[i]![j] = new VariantCoord { X = variantArrays[i][j].X, Y = variantArrays[i][j].Y };
            }
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
    /// Each entry is an array of possible tile variants for random selection.
    /// Null entries mean "use base tile coords" (shouldn't happen for compiled tiles).
    /// </summary>
    [JsonPropertyName("variants")]
    public VariantCoord[]?[] Variants { get; set; } = [];
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

