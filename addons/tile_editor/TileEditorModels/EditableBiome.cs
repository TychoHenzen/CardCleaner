#if TOOLS
using System.Collections.Generic;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Mutable biome data for editing in the tile editor
/// </summary>
public class EditableBiome
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float[] Signature { get; set; } = new float[8];
    public float BlockedPercentage { get; set; } = 0.2f;
    public Dictionary<string, float> PassableTiles { get; set; } = new();
    public Dictionary<string, float> BlockedTiles { get; set; } = new();

    public EditableBiome Clone()
    {
        return new EditableBiome
        {
            Id = Id,
            DisplayName = DisplayName,
            Signature = (float[])Signature.Clone(),
            BlockedPercentage = BlockedPercentage,
            PassableTiles = new Dictionary<string, float>(PassableTiles),
            BlockedTiles = new Dictionary<string, float>(BlockedTiles)
        };
    }
}
#endif
