using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Numerics;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public sealed record BiomeDefinition
{
    // Default values as constants
    private const string DefaultId = "plains";
    private const float DefaultBlockedPercentage = 0.3f;

    private float _blockedPercentage = DefaultBlockedPercentage;

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
        BlockedPercentage = blockedPercentage;
    }

    /// <summary>
    /// Factory method to create BiomeDefinition from JSON data
    /// </summary>
    public static BiomeDefinition FromData(string biomeId, BiomeData data)
    {
        var signature = new CardSignature(data.Signature);

        var passableTiles = new TilePool.Builder();
        foreach (var (tileId, weight) in data.PassableTiles)
            passableTiles.Add(tileId, weight);

        var blockedTiles = new TilePool.Builder();
        foreach (var (tileId, weight) in data.BlockedTiles)
            blockedTiles.Add(tileId, weight);

        return new BiomeDefinition(
            biomeId,
            signature,
            passableTiles.Build(),
            blockedTiles.Build(),
            data.BlockedPercentage);
    }

    public string Id { get; init; } = DefaultId;

    public CardSignature AffinitySignature { get; init; }

    public TilePool PassableTiles { get; init; } = TilePool.Empty;

    public TilePool BlockedTiles { get; init; } = TilePool.Empty;

    public float BlockedPercentage
    {
        get => _blockedPercentage;
        init => _blockedPercentage = CoreMath.Clamp(value, 0f, 1f);
    }

    public string? SelectPassableTile(IRandom rng) => PassableTiles.SelectRandom(rng);

    public string? SelectBlockedTile(IRandom rng) => BlockedTiles.SelectRandom(rng);
}
