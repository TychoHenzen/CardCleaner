using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// JSON deserialization model for biome definitions in tiles.json
/// </summary>
public sealed class BiomeData
{
    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("signature")]
    public float[] Signature { get; set; } = [];

    [JsonPropertyName("blockedPercentage")]
    public float BlockedPercentage { get; set; }

    [JsonPropertyName("passableTiles")]
    public Dictionary<string, float> PassableTiles { get; set; } = new();

    [JsonPropertyName("blockedTiles")]
    public Dictionary<string, float> BlockedTiles { get; set; } = new();
}
