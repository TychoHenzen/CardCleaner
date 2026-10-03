using System.Text.Json.Serialization;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

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
