namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Represents a tile variant with its selection weight.
/// Weight is used for both density calculation and relative probability within the group.
/// </summary>
public readonly struct VariantWeight
{
    public VariantWeight(string tileId, float weight)
    {
        TileId = tileId;
        Weight = weight;
    }

    /// <summary>
    /// The tile ID for this variant.
    /// </summary>
    public string TileId { get; }

    /// <summary>
    /// The selection weight (probability from TSX).
    /// Used for: (1) density calculation via max weight in group, (2) relative selection probability.
    /// </summary>
    public float Weight { get; }
}
