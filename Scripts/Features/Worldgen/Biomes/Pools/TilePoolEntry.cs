namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class TilePoolEntry
{
    // Default values as constants
    private const string DefaultTileId = "";
    private const float DefaultWeight = 1.0f;

    public TilePoolEntry() { }

    public TilePoolEntry(string tileId, float weight = DefaultWeight)
    {
        TileId = tileId;
        Weight = weight;
    }

    public string TileId { get; set; } = DefaultTileId;
    public float Weight { get; set; } = DefaultWeight;
}
