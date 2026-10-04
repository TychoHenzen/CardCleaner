using System.Text.Json.Serialization;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

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
