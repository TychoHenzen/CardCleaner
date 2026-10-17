using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Numerics;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public class BiomeDefinition
{
    // Default values as constants
    private const string DefaultId = "plains";
    private const float DefaultBlockedPercentage = 0.3f;

    public BiomeDefinition() { }

    public BiomeDefinition(
        string id,
        CardSignature affinitySignature,
        TilePool passableTiles,
        TilePool blockedTiles,
        float blockedPercentage = DefaultBlockedPercentage)
    {
        Id = id;
        AffinitySignature = affinitySignature;
        PassableTiles = passableTiles;
        BlockedTiles = blockedTiles;
        BlockedPercentage = CoreMath.Clamp(blockedPercentage, 0f, 1f);
    }

    /// <summary>
    /// Factory method to create BiomeDefinition from JSON data
    /// </summary>
    public static BiomeDefinition FromData(string biomeId, BiomeData data)
    {
        var signature = new CardSignature(data.Signature);

        var passableTiles = new TilePool();
        foreach (var (tileId, weight) in data.PassableTiles)
            passableTiles.Add(tileId, weight);

        var blockedTiles = new TilePool();
        foreach (var (tileId, weight) in data.BlockedTiles)
            blockedTiles.Add(tileId, weight);

        return new BiomeDefinition(
            biomeId,
            signature,
            passableTiles,
            blockedTiles,
            data.BlockedPercentage);
    }

    public string Id { get; set; } = DefaultId;

    public CardSignature AffinitySignature { get; set; } = new();

    public TilePool PassableTiles { get; set; } = new();

    public TilePool BlockedTiles { get; set; } = new();

    public float BlockedPercentage { get; set; } = DefaultBlockedPercentage;

    public string? SelectPassableTile(IRandom rng) => PassableTiles.SelectRandom(rng);

    public string? SelectBlockedTile(IRandom rng) => BlockedTiles.SelectRandom(rng);
}
